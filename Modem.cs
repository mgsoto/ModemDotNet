using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// Provides a base class for modem commands.
    /// </summary>
    public abstract class Modem
    {
        // Constants
        protected const byte SOH = 0x01; // Start Of Header
        protected const byte STX = 0x02; // Start Of Text
        protected const byte EOT = 0x04; // End Of Transmission
        protected const byte ACK = 0x06; // ACK
        protected const byte NAK = 0x15; // NACK
        protected const byte CAN = 0x18; // Cancel
        protected const byte CPMEOF = 0x1A;
        protected const int MAXERRORS = 10;
        protected const int WAIT_FOR_RECEIVER_TIMEOUT = 60_000;
        protected const int BLOCK_TIMEOUT = 1000;
        protected const byte ST_C = (byte)'C';
        protected const int SEND_BLOCK_TIMEOUT = 10_000;

        /// <summary>
        /// Transfers a file using the specified modem command.
        /// </summary>
        /// <param name="stream">The stream to perform the transfer on.</param>
        /// <param name="dataStream">The datastream to transfer.</param>
        /// <param name="fileName">The name of the file to transfer.</param>
        /// <param name="cancellationToken">Cancellation token for cancelling the task.</param>
        public abstract Task Send(Stream stream, Stream dataStream, string fileName, CancellationToken cancellationToken);

        /// <summary>
        /// Reads a byte from the stream.
        /// </summary>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="timer">Max time to wait to read from the buffer.</param>
        /// <param name="cancellationToken">Cancellation token used to cancel the task.</param>
        /// <returns>A single byte.</returns>
        protected async Task<byte> ReadByte(Stream channel, ModemTimer timer, CancellationToken cancellationToken)
        {
            byte? retVal = null;

            // Keep trying to read until we have some data or until it times out.
            while (!timer.Expired && !retVal.HasValue)
            {
                byte[] buffer = new byte[1];
                int numRead = await channel.ReadAsync(buffer, 0, 1, cancellationToken);

                if (numRead > 0)
                {
                    retVal = buffer[0];
                }

                Thread.Sleep(10);
            }

            // Error out if we didn't read anything.
            if (!retVal.HasValue)
            {
                throw new TimeoutException();
            }

            return retVal.Value;
        }

        /// <summary>
        /// Sends a block of data.
        /// </summary>
        /// <param name="channel">The channel to send on.</param>
        /// <param name="dataStream">The stream of data to send.</param>
        /// <param name="blockNumber">The block number</param>
        /// <param name="crc">CRC calculation to use.</param>
        /// <param name="cancellationToken">The cancellation token to use.</param>
        protected async Task SendBlocks(Stream channel, Stream dataStream, int blockNumber, ICrc crc, CancellationToken cancellationToken)
        {
            byte[] block = new byte[1024];
            int dataLength = await dataStream.ReadAsync(block, 0, 1024, cancellationToken);

            while (dataLength > 0)
            {
                await SendBlock(channel, blockNumber++, block, dataLength, crc, cancellationToken);
                dataLength = await dataStream.ReadAsync(block, 0, 1024, cancellationToken);
            }
        }

        /// <summary>
        /// Sends the EOT character.
        /// </summary>
        /// <param name="channel">The channel to send on.</param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        protected async Task SendEot(Stream channel, CancellationToken cancellationToken)
        {
            int errorCount = 0;
            ModemTimer timer = new ModemTimer(BLOCK_TIMEOUT);

            while (errorCount < 10)
            {
                await SendByte(channel, EOT, cancellationToken);

                try
                {
                    int character = await ReadByte(channel, timer, cancellationToken);

                    if (character == ACK)
                    {
                        return;
                    }
                    else if (character == CAN)
                    {
                        throw new IOException("Transmission terminated");
                    }
                }
                catch (TimeoutException)
                {
                    // Ignore timeout exceptions.
                }

                errorCount++;
            }
        }

        /// <summary>
        /// Waits for the receiver request token.
        /// </summary>
        /// <param name="channel">Channel to wait upon.</param>
        /// <param name="timer">Timer to use.</param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        /// <returns>True if we should continue, false if we should resend.</returns>
        protected async Task<bool> WaitReceiverRequest(Stream channel, ModemTimer timer, CancellationToken cancellationToken)
        {
            bool? retVal = null;

            while (!retVal.HasValue && !timer.Expired) // TODO: Validate this change.
            {
                int character = await ReadByte(channel, timer, cancellationToken);

                if (character == NAK)
                {
                    retVal = false;
                }
                if (character == ST_C)
                {
                    retVal = true;
                }
            }

            return retVal.Value;
        }

        /// <summary>
        /// Sends a byte to the channel.
        /// </summary>
        /// <param name="channel">The channel to use.</param>
        /// <param name="b">The byte to send.</param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        protected async Task SendByte(Stream channel, byte b, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[] { b };
            await channel.WriteAsync(buffer, 0, 1, cancellationToken);
            await channel.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Sends a block of data.
        /// </summary>
        /// <param name="channel">Channel to send the data on.</param>
        /// <param name="blockNumber">The block number we're sending.</param>
        /// <param name="block">The block contents to send.</param>
        /// <param name="dataLength">The length of the block.</param>
        /// <param name="crc">CRC calculation to use.</param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        protected async Task SendBlock(Stream channel, int blockNumber, byte[] block, int dataLength, ICrc crc, CancellationToken cancellationToken)
        {
            ModemTimer timer = new ModemTimer(SEND_BLOCK_TIMEOUT);
            int errorCount = 0;            

            if (dataLength < block.Length)
            {
                for (int k = dataLength; k < block.Length; k++)
                {
                    block[k] = CPMEOF;
                }
            }

            while (errorCount < MAXERRORS)
            {
                bool keepGoing = true;

                timer.Start();

                if (block.Length == 1024)
                {
                    channel.WriteByte(STX);
                }
                else //128
                {
                    channel.WriteByte(SOH);
                }

                channel.WriteByte((byte)blockNumber);
                channel.WriteByte((byte)~blockNumber);

                channel.Write(block, 0, block.Length);
                WriteCrc(channel, block, crc);
                channel.Flush();

                while (keepGoing)
                {
                    try
                    {
                        byte character = await ReadByte(channel, timer, cancellationToken);

                        if (character == ACK)
                        {
                            return;
                        }
                        else if (character == NAK)
                        {
                            errorCount++;
                            keepGoing = false;
                        }
                        else if (character == CAN)
                        {
                            throw new IOException("Transmission terminated");
                        }
                    }
                    catch (TimeoutException)
                    {
                        errorCount++;
                        keepGoing = false;
                    }
                }
            }

            throw new IOException("Too many errors caught, abandoning transfer");
        }

        /// <summary>
        /// Writes the CRC value to the stream.
        /// </summary>
        /// <param name="channel">Stream to write to.</param>
        /// <param name="block">Block to write.</param>
        /// <param name="crc">CRC calculation to use.</param>
        protected void WriteCrc(Stream channel, byte[] block, ICrc crc)
        {
            byte[] crcBytes = new byte[crc.Length];
            long crcValue = crc.Compute(block);

            for (int i = 0; i < crc.Length; i++)
            {
                crcBytes[crc.Length - i - 1] = (byte)((crcValue >> (8 * i)) & 0xFF);
            }

            channel.Write(crcBytes, 0, crcBytes.Length);
        }
    }
}