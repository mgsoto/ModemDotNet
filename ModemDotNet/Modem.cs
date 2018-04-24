using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        public abstract void Send(Stream stream, Stream dataStream, string fileName);
    }
}
