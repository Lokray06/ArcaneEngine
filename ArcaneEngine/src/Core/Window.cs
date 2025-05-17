// Arcane/Core/Window.cs
using System;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4; // Required for GL.Enable if you choose to explicitly enable Multisample

namespace Arcane.Core
{
    public class Window : IDisposable
    {
        private readonly GameWindow _gameWindow; // Renamed from gameWindow to follow C# conventions
        private string _baseTitle; // Renamed from baseTitle

        public bool IsOpen => _gameWindow != null && _gameWindow.IsVisible && !_gameWindow.IsExiting;
        public Action OnClose { get; set; }
        public int Width => _gameWindow.ClientSize.X;
        public int Height => _gameWindow.ClientSize.Y;

        public bool ShowFpsInTitle { get; set; } = false;

        public GameWindow NativeGameWindow => _gameWindow;

        /// <summary>
        /// Initializes a new instance of the <see cref="Window"/> class.
        /// </summary>
        /// <param name="width">The width of the window's client area.</param>
        /// <param name="height">The height of the window's client area.</param>
        /// <param name="title">The title of the window.</param>
        /// <param name="msaaSamples">The number of MSAA samples to request (e.g., 0 for no MSAA, 4 for 4x MSAA).
        /// Support depends on the GPU and drivers.</param>
        public Window(int width = 1024, int height = 768, string title = "Arcane Engine", int msaaSamples = 0)
        {
            _baseTitle = title;
            var nativeWindowSettings = new NativeWindowSettings()
            {
                ClientSize = new Vector2i(width, height),
                Title = _baseTitle,
                APIVersion = new Version(4, 0), // Or your desired OpenGL version
                Profile = ContextProfile.Core,
                Flags = ContextFlags.ForwardCompatible,
                StartVisible = true,
                WindowState = WindowState.Normal,
                NumberOfSamples = msaaSamples // <<< ADDED THIS LINE FOR MSAA
            };

            var gameWindowSettings = new GameWindowSettings()
            {
                UpdateFrequency = 0 // We drive updates in the Engine
            };

            _gameWindow = new GameWindow(gameWindowSettings, nativeWindowSettings);

            _gameWindow.Closing += (cancelEventArgs) => { OnClose?.Invoke(); };

            // After context creation, you might want to explicitly enable multisampling
            // if samples were successfully allocated. OpenTK usually handles this if the context
            // is created with NumberOfSamples > 0, but it doesn't hurt.
            if (msaaSamples > 0)
            {
                // Ensure the context is current before calling GL functions if this constructor
                // is called before MakeContextCurrent() in the Engine.
                // However, it's safer to do this in Engine.Initialize or Renderer.Init
                // after MakeContextCurrent has been called.
                // For now, we assume the context will be made current shortly after.
                // If you want to enable it here, you'd need to make context current first.
                // Example:
                // _gameWindow.MakeCurrent();
                // GL.Enable(EnableCap.Multisample);
                // _gameWindow.Context.MakeCurrent(null); // Release context if made current here
                // It's generally better to enable this in your Renderer's Init method.
                Debug.Log($"Window created with MSAA samples requested: {msaaSamples}. OpenGL's EnableCap.Multisample should be active.");
            }
        }

        public void MakeContextCurrent()
        {
            _gameWindow?.MakeCurrent();
        }

        public void SwapBuffers()
        {
            _gameWindow?.SwapBuffers();
        }

        public void ProcessEvents(double timeout)
        {
            _gameWindow?.ProcessEvents(timeout);
        }

        public bool VSync
        {
            get => _gameWindow?.VSync == VSyncMode.On;
            set
            {
                if (_gameWindow != null)
                {
                    _gameWindow.VSync = value ? VSyncMode.On : VSyncMode.Off;
                }
            }
        }

        public string Title
        {
            get => _baseTitle;
            set
            {
                _baseTitle = value;
                if (!ShowFpsInTitle)
                {
                    _gameWindow.Title = _baseTitle;
                }
            }
        }

        public void UpdateTitleWithFps(float fps)
        {
            if (ShowFpsInTitle && _gameWindow != null)
            {
                _gameWindow.Title = $"{_baseTitle} - FPS: {fps:F1}";
            }
            else if (_gameWindow != null && _gameWindow.Title != _baseTitle)
            {
                _gameWindow.Title = _baseTitle;
            }
        }

        public void Dispose()
        {
            _gameWindow?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
