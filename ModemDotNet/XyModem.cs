using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// https://github.com/aesirot/ymodem
    /// </summary>
    public sealed class YModem : Modem
    {
        public override async Task Send(Stream channel, Stream dataStream, string fileName, CancellationToken cancellationToken)
        {
            ModemTimer timer = new ModemTimer(WAIT_FOR_RECEIVER_TIMEOUT);
            timer.Start();
            bool useCrc16 = await WaitReceiverRequest(channel, timer, cancellationToken);
            ICrc crc = await WaitReceiverRequest(channel, timer, cancellationToken) ? Crc.Crc16 : Crc.Crc8;

            string fileNameString = $"{fileName.ToLower()}";
            byte[] fileNameBytes = new byte[128];
            Encoding.UTF8.GetBytes(fileNameString, 0, fileNameString.Length, fileNameBytes, 0);

            await SendBlock(channel, 0, fileNameBytes, 128, crc, cancellationToken);

            await WaitReceiverRequest(channel, timer, cancellationToken);
            //send data
            await SendDataBlocks(channel, dataStream, 1, crc, cancellationToken);

            await SendEot(channel, cancellationToken);
        }

        private async Task SendDataBlocks(Stream channel, Stream dataStream, int blockNumber, ICrc crc, CancellationToken cancellationToken)
        {
            int dataLength;
            byte[] block = new byte[1024];

            while ((dataLength = dataStream.Read(block, 0, 1024)) > 0)
            {
                await SendBlock(channel, blockNumber++, block, dataLength, crc, cancellationToken);
            }
        }

        private async Task SendEot(Stream channel, CancellationToken cancellationToken)
        {
            int errorCount = 0;
            ModemTimer timer = new ModemTimer(BLOCK_TIMEOUT);
            int character;
            while (errorCount < 10)
            {
                SendByte(channel, EOT);
                try
                {
                    character = await ReadByte(channel, timer, cancellationToken);

                    if (character == ACK)
                    {
                        return;
                    }
                    else if (character == CAN)
                    {
                        throw new IOException("Transmission terminated");
                    }
                }
                catch (TimeoutException ignored)
                {
                }
                errorCount++;
            }
        }

        private async Task<bool> WaitReceiverRequest(Stream channel, ModemTimer timer, CancellationToken cancellationToken)
        {
        int character;
            while (true) {
            try {
                character = await ReadByte(channel, timer, cancellationToken);
                if (character == NAK)
                    return false;
                if (character == ST_C) {
                    return true;
                }
            } catch (TimeoutException e) {
                throw new IOException("Timeout waiting for receiver");
            }
        }
    }

        protected void SendByte(Stream channel, byte b)
        {
            channel.WriteByte(b);
            channel.Flush();
        }

        private async Task SendBlock(Stream channel, int blockNumber, byte[] block, int dataLength, ICrc crc, CancellationToken cancellationToken)
        {
            int errorCount;
            int character;
            ModemTimer timer = new ModemTimer(SEND_BLOCK_TIMEOUT);

            if (dataLength < block.Length)
            {
                for(int k = dataLength; k < block.Length; k++)
                {
                    block[k] = CPMEOF;
                }
            }
            errorCount = 0;

            while (errorCount < MAXERRORS)
            {
                timer.Start();

                if (block.Length == 1024)
                    channel.WriteByte(STX);
                else //128
                    channel.WriteByte(SOH);
                channel.WriteByte((byte)blockNumber);
                channel.WriteByte((byte)~blockNumber);

                channel.Write(block, 0, block.Length);
                WriteCrc(channel, block, crc);
                channel.Flush();

                while (true)
                {
                    try
                    {
                        character = await ReadByte(channel, timer, cancellationToken);
                        if (character == ACK)
                        {
                            return;
                        }
                        else if (character == NAK)
                        {
                            errorCount++;
                            break;
                        }
                        else if (character == CAN)
                        {
                            throw new IOException("Transmission terminated");
                        }
                    }
                    catch (TimeoutException e)
                    {
                        errorCount++;
                        break;
                    }
                }

            }

            throw new IOException("Too many errors caught, abandoning transfer");
        }

        private void WriteCrc(Stream channel, byte[] block, ICrc crc)
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
