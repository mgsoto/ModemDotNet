using System;
using System.Collections.Generic;
using System.Text;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// Provides some timers for modem timing.
    /// </summary>
    public class ModemTimer
    {
        /// <summary>
        /// Gets the start time.
        /// </summary>
        public long StartTime => _startTime;

        /// <summary>
        /// Start time of the timer.
        /// </summary>
        private long _startTime = 0;

        /// <summary>
        /// Gets the stope time.
        /// </summary>
        public long StopTime => _stopTime;

        /// <summary>
        /// Stop time of the timer.
        /// </summary>
        private long _stopTime = 0;

        /// <summary>
        /// Timeout of the timer.
        /// </summary>
        public long Timeout { get; set; } = 0;

        /// <summary>
        /// Gets if the timer is running or not.
        /// </summary>
        public bool Running => StopTime != 0;

        /// <summary>
        /// Gets the total time of the timer.
        /// </summary>
        public long TotalTime => _stopTime - _startTime;

        /// <summary>
        /// Gets a value indicating if the timeout is expired.
        /// </summary>
        public bool Expired => DateTime.Now.ToUnixTime() > _startTime + Timeout;

        /// <summary>
        /// Initializes a new instance of the ModemTimer class.
        /// </summary>
        /// <param name="timeout">Timeout value.</param>
        public ModemTimer(long timeout)
        {
            Timeout = timeout;
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        public void Start()
        {
            _startTime = DateTime.Now.ToUnixTime();
            _stopTime = 0;
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Stop()
        {
            _stopTime = DateTime.Now.ToUnixTime();
        }
    }
}
