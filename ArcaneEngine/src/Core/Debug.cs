using System.ComponentModel;
using OpenTK.Graphics.ES11;

namespace Arcane.Core
{
    public class Debug
    {
        // --- FPS Calculation Fields ---
        public static float Fps { get; private set; } = 0f;
        private static double _lastFpsCalcSimTime = 0.0;
        private static long _framesAtLastFpsCalc = 0;
        private const double FPS_CALC_INTERVAL_SECONDS = 0.5; // How often to update FPS

        public static void Log(string message)
        {
            Console.WriteLine($"[LOG] {message}");
        }

        public static void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARN] {message}");
            Console.ResetColor();
        }

        public static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        }

        public static void LogEngineState(long totalFrames, long totalFixedFrames) // Example
        {
            // This is just an example; you can customize what state to log
            // Console.WriteLine($"FPS: {Fps:F1}, Frame: {totalFrames}, FixedFrame: {totalFixedFrames}, SimTime: {Time.simulationUptimeSeconds:F2}s");
        }

        /// <summary>
        /// Calculates and updates the FPS.
        /// Should be called regularly (e.g., once per frame or per fixed update).
        /// </summary>
        /// <param name="currentTotalFrames">The total number of frames rendered so far.</param>
        /// <param name="currentSimulationTimeSeconds">The current total simulation time.</param>
        public static void UpdateFPS(long currentTotalFrames, double currentSimulationTimeSeconds)
        {
            if (currentSimulationTimeSeconds >= _lastFpsCalcSimTime + FPS_CALC_INTERVAL_SECONDS)
            {
                double elapsedTime = currentSimulationTimeSeconds - _lastFpsCalcSimTime;
                long elapsedFrames = currentTotalFrames - _framesAtLastFpsCalc;

                if (elapsedTime > 0.000001)
                {
                    Fps = (float)(elapsedFrames / elapsedTime);
                }

                _framesAtLastFpsCalc = currentTotalFrames;
                _lastFpsCalcSimTime = currentSimulationTimeSeconds;
            }
        }

        public static void ResetFPSMetrics(long initialFrames, double initialSimTime)
        {
            Fps = 0f;
            _framesAtLastFpsCalc = initialFrames;
            _lastFpsCalcSimTime = initialSimTime;
        }
    }
}