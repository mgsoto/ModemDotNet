using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// Provides extension methods for modem commands.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Instance of the YModem class.
        /// </summary>
        private static readonly YModem _yModem = new YModem();

        /// <summary>
        /// Transfers a file using YModem.
        /// </summary>
        /// <param name="stream">The stream to perform the transfer on.</param>
        /// <param name="dataStream">The datastream to transfer.</param>
        /// <param name="fileName">The name of the file to transfer.</param>
        public static async Task SendYModem(this Stream stream, Stream dataStream, string fileName)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            await SendYModem(stream, dataStream, fileName, cts.Token);
        }

        /// <summary>
        /// Transfers a file using YModem.
        /// </summary>
        /// <param name="stream">The stream to perform the transfer on.</param>
        /// <param name="dataStream">The datastream to transfer.</param>
        /// <param name="fileName">The name of the file to transfer.</param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        public static async Task SendYModem(this Stream stream, Stream dataStream, string fileName, CancellationToken cancellationToken)
        {
            await _yModem.Send(stream, dataStream, fileName, cancellationToken);
        }
    }
}
