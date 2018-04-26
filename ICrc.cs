using System;
using System.Collections.Generic;
using System.Text;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// Interface for CRC calculations.
    /// </summary>
    public interface ICrc
    {
        /// <summary>
        /// Gets the length of the CRC bytes.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Calculates the CRC value.
        /// </summary>
        /// <param name="block">Block of data to calculate.</param>
        /// <returns>The computed CRC value.</returns>
        long Compute(byte[] block);
    }
}
