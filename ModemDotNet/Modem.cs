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
                if (channel.Length - channel.Position > 0)
                {
                    byte[] buffer = new byte[1];
                    int numRead = await channel.ReadAsync(buffer, 0, 1, cancellationToken);

                    if(numRead > 0)
                    {
                        retVal = buffer[0];
                    }
                }

                Thread.Sleep(10);
            }

            // Error out if we didn't read anything.
            if(!retVal.HasValue)
            {
                throw new TimeoutException();
            }

            return retVal.Value;
        }
    }
}