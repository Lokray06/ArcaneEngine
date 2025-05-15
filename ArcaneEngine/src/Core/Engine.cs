using Arcane.SceneSystem;
using Arcane.Renderering;
using Arcane.Components;

namespace Arcane.Core
{
    public class Engine
    {
        public static Renderer RenderPipeline;
        public static bool IsRunning { get; private set; } = false;
        public static double TimeStepSeconds { get; set; } = 1.0 / 60.0;

        private long _totalFrames = 0;
        private long _totalFixedFrames = 0;

        private Window _window;
        private double _accumulator = 0.0;

        private const double MIN_DELTA_TIME_SECONDS = 0.0;
        private const double MAX_DELTA_TIME_SECONDS = 0.5;

        private GameObject _currentCameraForRendering;

        public Engine() { }

        public void Initialize(bool showFpsInTitle = false) // Added parameter
        {
            Debug.Log("Engine Initialize: Starting...");
            _window = new Window(1024, 768, "Arcane Engine");
            _window.ShowFpsInTitle = showFpsInTitle; // Set the flag
            _window.OnClose = () => { IsRunning = false; };
            _window.MakeContextCurrent();

            if (RenderPipeline == null)
            {
                Debug.Log("Engine Initialize: No render pipeline set. Initializing default Radiance renderer.");
                RenderPipeline = new Radiance();
            }
            RenderPipeline.Init();

            Time.ResetUptime();
            Debug.ResetFPSMetrics(0, Time.simulationUptimeSeconds);
            _totalFrames = 0;
            _totalFixedFrames = 0;
            _accumulator = 0.0;

            IsRunning = true;
            Debug.Log("Engine Initialize: Complete.");
        }

        public void RunLoop()
        {
            if (!IsRunning || _window == null) return;

            Debug.Log("Engine RunLoop: Starting main loop...");
            while (IsRunning && _window.IsOpen)
            {
                _window.ProcessEvents(0.0);
                UpdateTimersAndAccumulator();

                while (_accumulator >= TimeStepSeconds)
                {
                    SceneManager.FixedUpdateCurrentScene();
                    Time.simulationUptimeSeconds += TimeStepSeconds;
                    _accumulator -= TimeStepSeconds;
                    _totalFixedFrames++;
                }

                SceneManager.UpdateCurrentScene();
                RenderFrame();

                _totalFrames++;
                Debug.UpdateFPS(_totalFrames, Time.simulationUptimeSeconds);

                // Update window title with FPS if enabled
                if (_window.ShowFpsInTitle)
                {
                    _window.UpdateTitleWithFps(Debug.Fps);
                }
            }
            CleanUp();
        }

        private void UpdateTimersAndAccumulator()
        {
            Time.UpdateFrameStart();
            double currentRawDeltaTimeSeconds = Time.rawDeltaTimeSeconds;
            double clampedDeltaTimeSeconds = currentRawDeltaTimeSeconds;

            if (MIN_DELTA_TIME_SECONDS > 0.0 && clampedDeltaTimeSeconds < MIN_DELTA_TIME_SECONDS)
            {
                clampedDeltaTimeSeconds = MIN_DELTA_TIME_SECONDS;
            }
            if (clampedDeltaTimeSeconds > MAX_DELTA_TIME_SECONDS)
            {
                clampedDeltaTimeSeconds = MAX_DELTA_TIME_SECONDS;
            }
            Time.deltaTimeSeconds = clampedDeltaTimeSeconds;
            _accumulator += Time.deltaTimeSeconds;
        }

        private void RenderFrame()
        {
            if (RenderPipeline == null) return;
            _currentCameraForRendering = SceneManager.MainCamera;

            if (_currentCameraForRendering != null)
            {
                var camComp = _currentCameraForRendering.GetComponent<CameraComponent>();
                if (camComp != null && _window != null)
                {
                    float currentAspect = (float)_window.Width / Math.Max(1, _window.Height);
                    if (Math.Abs(camComp.AspectRatio - currentAspect) > 0.001f)
                    {
                        camComp.AspectRatio = currentAspect;
                    }
                }
            }
            RenderPipeline.Render(_currentCameraForRendering, SceneManager.ActiveScene);
            _window.SwapBuffers();
        }

        private void CleanUp()
        {
            Debug.Log("Engine CleanUp: Disposing resources...");
            SceneManager.DestroyCurrentScene();
            RenderPipeline?.Cleanup();
            _window?.Dispose();
            IsRunning = false;
            Debug.Log("Engine CleanUp: Complete.");
        }
    }
}
