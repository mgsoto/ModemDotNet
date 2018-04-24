using System;
using System.Collections.Generic;
using System.Text;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// Extension methods for this project.
    /// </summary>
    internal static class InternalExtensions
    {
        /// <summary>
        /// Converts a datetime to unit time as a string.
        /// </summary>
        /// <param name="date">Datetime to convert.</param>
        /// <returns>Unix time as a string.</returns>
        public static string ToUnixTimeString(this DateTime date)
        {
            var unixTime = ToUnixTime(date);
            return Convert.ToString(unixTime, 8);
        }

        /// <summary>
        /// Converts a datetime to unix time.
        /// </summary>
        /// <param name="date">Datetime to convert.</param>
        /// <returns>Converted time as unix time.</returns>
        public static long ToUnixTime(this DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date - epoch).Seconds);
        }
    }
}
