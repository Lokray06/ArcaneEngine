// ArcaneEngine/src/Core/Input.cs
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework; // For Keys enum and KeyboardState
using OpenTK.Mathematics; // For Vector2
using System.Collections.Generic;

// Alias for OpenTK Keys and MouseButton to avoid naming conflicts if KeyCode/MouseButtonCode were nested or in same file.
using OTKKeys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using OTKMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace Arcane.Core
{
    /// <summary>
    /// Provides static methods to access input from keyboard and mouse.
    /// Must be initialized by the Engine and updated every frame.
    /// </summary>
    public static class Input
    {
        private static GameWindow _windowInstance; // Renamed from windowInstance
        private static KeyboardState _currentKeyboardState; // Renamed from currentKeyboardState
        private static KeyboardState _previousKeyboardState; // Renamed from previousKeyboardState

        private static MouseState _currentMouseState; // Renamed from currentMouseState
        private static MouseState _previousMouseState; // Renamed from previousMouseState

        private static Vector2 _mousePositionInternal; // Renamed from mousePositionInternal
        private static Vector2 _lastMousePositionInternal; // Renamed from lastMousePositionInternal

        /// <summary>
        /// Initializes the Input system with the game window.
        /// Should be called once by the Engine after the window is created.
        /// </summary>
        /// <param name="window">The active GameWindow instance.</param>
        public static void Initialize(GameWindow window)
        {
            _windowInstance = window;
            if (_windowInstance != null)
            {
                // Initialize keyboard states
                _currentKeyboardState = _windowInstance.KeyboardState.GetSnapshot();
                _previousKeyboardState = _currentKeyboardState;

                // Initialize mouse states
                _currentMouseState = _windowInstance.MouseState.GetSnapshot();
                _previousMouseState = _currentMouseState;

                _mousePositionInternal = _currentMouseState.Position;
                _lastMousePositionInternal = _mousePositionInternal;
            }
            else
            {
                Debug.LogError("Input.Initialize: GameWindow instance is null. Input will not function.");
            }
        }

        /// <summary>
        /// Updates the input states.
        /// Should be called once per frame by the Engine.
        /// </summary>
        public static void Update()
        {
            if (_windowInstance == null) return;

            // Update keyboard states
            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = _windowInstance.KeyboardState.GetSnapshot();

            // Update mouse states
            _previousMouseState = _currentMouseState;
            _currentMouseState = _windowInstance.MouseState.GetSnapshot();

            // Update mouse position and delta
            _lastMousePositionInternal = _mousePositionInternal;
            _mousePositionInternal = _currentMouseState.Position;
        }

        /// <summary>
        /// Gets a value indicating whether the game window currently has input focus.
        /// </summary>
        public static bool IsWindowFocused => _windowInstance?.IsFocused ?? false;


        // --- Keyboard Methods using Arcane.Core.KeyCode ---
        public static bool GetKey(KeyCode key)
        {
            if (!IsWindowFocused || _windowInstance == null) return false; // Check focus
            return _currentKeyboardState.IsKeyDown((OTKKeys)key);
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (!IsWindowFocused || _windowInstance == null) return false; // Check focus
            OTKKeys otkKey = (OTKKeys)key;
            return _currentKeyboardState.IsKeyDown(otkKey) && !_previousKeyboardState.IsKeyDown(otkKey);
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (!IsWindowFocused || _windowInstance == null) return false; // Check focus
            OTKKeys otkKey = (OTKKeys)key;
            return !_currentKeyboardState.IsKeyDown(otkKey) && _previousKeyboardState.IsKeyDown(otkKey);
        }

        // --- Raw Keyboard Methods (using OpenTK Keys) ---
        // Consider making these internal or removing if KeyCode is always preferred externally
        public static bool GetKeyRaw(OTKKeys key)
        {
            if (!IsWindowFocused || _windowInstance == null) return false;
            return _currentKeyboardState.IsKeyDown(key);
        }
        public static bool GetKeyDownRaw(OTKKeys key)
        {
            if (!IsWindowFocused || _windowInstance == null) return false;
            return _currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }
        public static bool GetKeyUpRaw(OTKKeys key)
        {
            if (!IsWindowFocused || _windowInstance == null) return false;
            return !_currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyDown(key);
        }


        // --- Mouse Methods ---
        public static bool GetMouseButton(OTKMouseButton button)
        {
            // Mouse button clicks might be desired even if window isn't "keyboard" focused,
            // e.g., clicking to regain focus. So, IsWindowFocused check is optional here.
            // However, for consistency or to prevent actions if window is truly inactive, you can add it.
            if (_windowInstance == null) return false;
            return _currentMouseState.IsButtonDown(button);
        }

        public static bool GetMouseButtonDown(OTKMouseButton button)
        {
            if (_windowInstance == null) return false;
            // Check focus for GetMouseButtonDown to ensure the "down" event is relevant to this window.
            if (!_windowInstance.IsFocused && button == OTKMouseButton.Left) // Allow left click to regain focus without triggering game actions immediately
            {
                // If you want to allow left-click to *only* focus without triggering game actions on that specific click,
                // this logic would need to be more complex, possibly involving event handling in the Window class.
                // For now, if not focused, GetMouseButtonDown for Left might still be true if clicked to focus.
                // A common pattern is to check IsWindowFocused *before* calling GetMouseButtonDown in game logic.
            }
            return _currentMouseState.IsButtonDown(button) && !_previousMouseState.IsButtonDown(button);
        }

        public static bool GetMouseButtonUp(OTKMouseButton button)
        {
            if (_windowInstance == null) return false;
            return !_currentMouseState.IsButtonDown(button) && _previousMouseState.IsButtonDown(button);
        }

        public static Vector2 MousePosition
        {
            get
            {
                if (_windowInstance == null) return Vector2.Zero;
                return _mousePositionInternal;
            }
        }

        public static Vector2 MouseDelta
        {
            get
            {
                if (!IsWindowFocused || _windowInstance == null) return Vector2.Zero; // Return zero if not focused
                return _mousePositionInternal - _lastMousePositionInternal;
            }
        }

        public static Vector2 MouseScrollDelta
        {
            get
            {
                if (!IsWindowFocused || _windowInstance == null) return Vector2.Zero;
                return _currentMouseState.ScrollDelta;
            }
        }

        public static bool IsCursorVisible
        {
            get => _windowInstance?.CursorState == OpenTK.Windowing.Common.CursorState.Normal;
            set
            {
                if (_windowInstance != null)
                {
                    _windowInstance.CursorState = value ? OpenTK.Windowing.Common.CursorState.Normal : OpenTK.Windowing.Common.CursorState.Hidden;
                }
            }
        }

        public static bool IsCursorGrabbed
        {
            get => _windowInstance?.CursorState == OpenTK.Windowing.Common.CursorState.Grabbed;
            set
            {
                if (_windowInstance != null)
                {
                    _windowInstance.CursorState = value ? OpenTK.Windowing.Common.CursorState.Grabbed : OpenTK.Windowing.Common.CursorState.Normal;
                }
            }
        }
        public static void SetMousePosition(Vector2 position)
        {
            if (_windowInstance != null)
            {
                _windowInstance.MousePosition = position;
                _mousePositionInternal = position;
                _lastMousePositionInternal = position;
            }
        }
    }
}
