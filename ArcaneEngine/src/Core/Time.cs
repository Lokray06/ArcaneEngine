using System.Diagnostics;

namespace Arcane.Core
{
    public static class Time
    {
        // Time values are now in seconds (double precision)
        public static double uptimeSeconds { get; internal set; }
        public static double simulationUptimeSeconds { get; internal set; } // Managed by Engine's FixedUpdate
        public static double deltaTimeSeconds { get; internal set; }       // Clamped version, set by Engine
        public static double rawDeltaTimeSeconds { get; internal set; }    // Unclamped, calculated here

        private static readonly Stopwatch stopwatch = new Stopwatch();
        private static long lastFrameTimestampTicks = 0;
        private static readonly double ticksPerSecond; // Cached for performance

        static Time()
        {
            if (!Stopwatch.IsHighResolution)
            {
                // Log a warning or handle the case where high-resolution timer is not available
                System.Console.WriteLine("Warning: High-resolution stopwatch not available. Timing precision may be limited.");
            }
            ticksPerSecond = (double)Stopwatch.Frequency;
            stopwatch.Start();
            // Initialize last frame timestamp to current time to avoid a huge first delta time
            lastFrameTimestampTicks = stopwatch.ElapsedTicks;
        }

        /// <summary>
        /// Called by the Engine at the very start of each frame to update rawDeltaTimeSeconds and uptimeSeconds.
        /// </summary>
        internal static void UpdateFrameStart()
        {
            long currentFrameTimestampTicks = stopwatch.ElapsedTicks;
            rawDeltaTimeSeconds = (double)(currentFrameTimestampTicks - lastFrameTimestampTicks) / ticksPerSecond;
            lastFrameTimestampTicks = currentFrameTimestampTicks;

            // Prevent negative or excessively large delta time on the first few frames or after a major stall
            // A MAXDELTATIME check for rawDeltaTimeSeconds might also be good here if not done elsewhere before use.
            if (rawDeltaTimeSeconds < 0) rawDeltaTimeSeconds = 0;

            uptimeSeconds += rawDeltaTimeSeconds;
        }

        /// <summary>
        /// Resets the main uptime. Called during Engine.Init typically.
        /// </summary>
        internal static void ResetUptime()
        {
            uptimeSeconds = 0.0;
            // Re-prime last frame timestamp to avoid large delta after reset during an ongoing stopwatch
            lastFrameTimestampTicks = stopwatch.ElapsedTicks;
            // simulationUptimeSeconds is reset by the Engine.
        }

        // The concept of "Time.Now()" returning milliseconds is less central
        // to the engine's loop now. If needed for other purposes:
        public static long GetCurrentMilliseconds()
        {
            return stopwatch.ElapsedMilliseconds;
        }

        public static double GetCurrentSeconds()
        {
            return (double)stopwatch.ElapsedTicks / ticksPerSecond;
        }
    }
}