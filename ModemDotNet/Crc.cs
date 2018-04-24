using System;
using System.Collections.Generic;
using System.Text;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// Provides CRC calculations.
    /// </summary>
    public abstract class Crc : ICrc
    {
        /// <summary>
        /// Instance of the CRC 16 calculation class.
        /// </summary>
        public static ICrc Crc16 => _crc16;

        private static readonly Crc16 _crc16 = new Crc16();

        /// <summary>
        /// Instance of the CRC 8 calculation class.
        /// </summary>
        public static ICrc Crc8 => _crc8;

        private static readonly Crc8 _crc8 = new Crc8();

        /// <summary>
        /// Gets the length of the CRC bytes.
        /// </summary>
        public abstract int Length { get; }

        /// <summary>
        /// Calculates the CRC value.
        /// </summary>
        /// <param name="block">Block of data to calculate.</param>
        /// <returns>The computed CRC value.</returns>
        public abstract long Compute(byte[] block);
    }
}
