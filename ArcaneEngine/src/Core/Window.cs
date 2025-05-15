// Arcane/Core/Window.cs
using System;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Mathematics;

namespace Arcane.Core
{
    public class Window : IDisposable
    {
        private readonly GameWindow _gameWindow;
        private string _baseTitle; // To store the original title

        public bool IsOpen => _gameWindow != null && _gameWindow.IsVisible && !_gameWindow.IsExiting;
        public Action OnClose { get; set; }
        public int Width => _gameWindow.ClientSize.X; // Use ClientSize for OpenTK 4+
        public int Height => _gameWindow.ClientSize.Y;

        public bool ShowFpsInTitle { get; set; } = false; // Flag to control FPS display

        public Window(int width = 1024, int height = 768, string title = "Arcane Engine")
        {
            _baseTitle = title;
            var nativeWindowSettings = new NativeWindowSettings()
            {
                ClientSize = new Vector2i(width, height), // Corrected from Size
                Title = _baseTitle,
                APIVersion = new Version(3, 3),
                Profile = ContextProfile.Core,
                Flags = ContextFlags.ForwardCompatible,
                StartVisible = true,
                WindowState = WindowState.Normal,
            };

            var gameWindowSettings = new GameWindowSettings()
            {
                UpdateFrequency = 0  // We drive updates
            };

            _gameWindow = new GameWindow(gameWindowSettings, nativeWindowSettings);

            _gameWindow.Closing += (cancelEventArgs) => { OnClose?.Invoke(); };
            // We don't need Load, Unload, Resize, UpdateFrame, RenderFrame events here
            // if the Engine and Renderer manage these aspects.
            // Resize events might be useful if the Renderer needs to update viewport directly from here.
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
            get => _baseTitle; // Return the base title
            set // Allow changing the base title
            {
                _baseTitle = value;
                if (!ShowFpsInTitle) // If not showing FPS, update window title directly
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
            else if (_gameWindow != null && _gameWindow.Title != _baseTitle) // Reset if flag turned off
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