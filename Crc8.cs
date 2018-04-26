using System;
using System.Collections.Generic;
using System.Text;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// Provides an instance of a CRC 8 calculation.
    /// </summary>
    public class Crc8 : Crc
    {
        /// <summary>
        /// Gets the length of the CRC bytes.
        /// </summary>
        public override int Length => 2;

        /// <summary>
        /// Calculates the CRC value.
        /// </summary>
        /// <param name="block">Block of data to calculate.</param>
        /// <returns>The computed CRC value.</returns>
        public override long Compute(byte[] block)
        {
            byte checksum = 0;

            for (int i = 0; i < block.Length; i++)
            {
                checksum += block[i];
            }
            return checksum;
        }
    }
}
