using Arcane.SceneSystem;
using Arcane.Renderering;
using Arcane.Components;
using Arcane.Rendering;
using OpenTK.Graphics.OpenGL4;

namespace Arcane.Core
{
    public class Engine
    {
        public static Renderer RenderPipeline;
        public static bool IsRunning { get; private set; } = false;
        public static double TimeStepSeconds { get; set; } = 1.0 / 60.0;

        private long totalFrames = 0;
        private long totalFixedFrames = 0;

        private Window window;
        private double accumulator = 0.0;

        private const double MINDELTATIMESECONDS = 0.0;
        private const double MAXDELTATIMESECONDS = 0.5;

        private GameObject currentCameraForRendering;

        public Engine() { }

        public void Initialize(int msaaSamples, bool showFpsInTitle = false) // Added parameter
        {
            Debug.Log("Engine Initialize: Starting...");

            Debug.Log("Initializing Asset Manager...");
            string projectRootPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            string assetsFolderPath = Path.Combine(projectRootPath, "TestGame", "Assets");


            // Ensure the path is full and normalized, though Combine usually handles it well.
            assetsFolderPath = Path.GetFullPath(assetsFolderPath);

            Arcane.Core.Debug.Log($"AssetRegistry: Attempting to scan assets from: {assetsFolderPath}");
            Arcane.AssetManager.AssetRegistry.ScanAssets(assetsFolderPath);

            window = new Window(1024, 768, "Arcane Engine", msaaSamples);
            window.ShowFpsInTitle = showFpsInTitle; // Set the flag
            window.OnClose = () => { IsRunning = false; };
            window.MakeContextCurrent();

            Input.Initialize(window.NativeGameWindow);

            Material.InitializeDefaultTextures();

            if (RenderPipeline == null)
            {
                Debug.Log("Engine Initialize: No render pipeline set. Initializing default Radiance renderer.");
                RenderPipeline = new Radiance();
            }
            RenderPipeline.Init();

            Time.ResetUptime();
            Debug.ResetFPSMetrics(0, Time.simulationUptimeSeconds);
            totalFrames = 0;
            totalFixedFrames = 0;
            accumulator = 0.0;

            IsRunning = true;
            Debug.Log("Engine Initialize: Complete.");
        }

        public void RunLoop()
        {
            if (!IsRunning || window == null) return;

            Debug.Log("Engine RunLoop: Starting main loop...");
            while (IsRunning && window.IsOpen)
            {
                window.ProcessEvents(0.0);
                UpdateTimersAndAccumulator();
                Input.Update();

                while (accumulator >= TimeStepSeconds)
                {
                    SceneManager.FixedUpdateCurrentScene();
                    Time.simulationUptimeSeconds += TimeStepSeconds;
                    accumulator -= TimeStepSeconds;
                    totalFixedFrames++;
                }

                SceneManager.UpdateCurrentScene();
                RenderFrame();

                totalFrames++;
                Debug.UpdateFPS(totalFrames, Time.simulationUptimeSeconds);

                // Update window title with FPS if enabled
                if (window.ShowFpsInTitle)
                {
                    window.UpdateTitleWithFps(Debug.Fps);
                }
            }
            CleanUp();
        }

        private void UpdateTimersAndAccumulator()
        {
            Time.UpdateFrameStart();
            double currentRawDeltaTimeSeconds = Time.rawDeltaTimeSeconds;
            double clampedDeltaTimeSeconds = currentRawDeltaTimeSeconds;

            if (MINDELTATIMESECONDS > 0.0 && clampedDeltaTimeSeconds < MINDELTATIMESECONDS)
            {
                clampedDeltaTimeSeconds = MINDELTATIMESECONDS;
            }
            if (clampedDeltaTimeSeconds > MAXDELTATIMESECONDS)
            {
                clampedDeltaTimeSeconds = MAXDELTATIMESECONDS;
            }
            Time.deltaTimeSeconds = clampedDeltaTimeSeconds;
            accumulator += Time.deltaTimeSeconds;
        }

        private void RenderFrame()
        {
            if (RenderPipeline == null) return;

            GL.Viewport(0, 0, window.Width, window.Height);
            GLDebug.CheckError("Engine.RenderFrame - After GL.Viewport reset");

            currentCameraForRendering = SceneManager.MainCamera;

            if (currentCameraForRendering != null)
            {
                var camComp = currentCameraForRendering.GetComponent<CameraComponent>();
                if (camComp != null && window != null)
                {
                    float currentAspect = (float)window.Width / Math.Max(1, window.Height);
                    if (Math.Abs(camComp.AspectRatio - currentAspect) > 0.001f)
                    {
                        camComp.AspectRatio = currentAspect;
                    }
                }
            }
            RenderPipeline.Render(currentCameraForRendering, SceneManager.ActiveScene);
            window.SwapBuffers();
        }

        private void CleanUp()
        {
            Debug.Log("Engine CleanUp: Disposing resources...");
            Material.DisposeDefaultTextures();
            SceneManager.DestroyCurrentScene();
            RenderPipeline?.Cleanup();
            window?.Dispose();
            IsRunning = false;
            Debug.Log("Engine CleanUp: Complete.");
        }
    }
}
