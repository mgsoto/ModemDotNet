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
    /// Implements Y Modem.
    /// </summary>
    public sealed class YModem : Modem
    {
        /// <summary>
        /// Transfers a file using Y Modem.
        /// </summary>
        /// <param name="stream">The stream to perform the transfer on.</param>
        /// <param name="dataStream">The datastream to transfer.</param>
        /// <param name="fileName">The name of the file to transfer.</param>
        /// <param name="cancellationToken">Cancellation token for cancelling the task.</param>
        public override async Task Send(Stream channel, Stream dataStream, string fileName, CancellationToken cancellationToken)
        {
            // Setup the timer.
            ModemTimer timer = new ModemTimer(WAIT_FOR_RECEIVER_TIMEOUT);
            timer.Start();

            // Choose a CRC calculation.
            bool useCrc16 = await WaitReceiverRequest(channel, timer, cancellationToken);
            ICrc crc = await WaitReceiverRequest(channel, timer, cancellationToken) ? Crc.Crc16 : Crc.Crc8;

            // Convert the filename to bytes.
            string fileNameString = $"{fileName.ToLower()}";
            byte[] fileNameBytes = new byte[128];
            Encoding.UTF8.GetBytes(fileNameString, 0, fileNameString.Length, fileNameBytes, 0);

            // Send the filename block.
            await SendBlock(channel, 0, fileNameBytes, 128, crc, cancellationToken);

            // Wait till the device says it's good.
            await WaitReceiverRequest(channel, timer, cancellationToken);

            // Send the file contents.
            await SendBlocks(channel, dataStream, 1, crc, cancellationToken);

            // Send the EOT (we're done sending data) character.
            await SendEot(channel, cancellationToken);
        }
    }
}
