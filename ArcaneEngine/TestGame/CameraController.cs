using Arcane.Core; // For Input, Time, Debug, KeyCode
using Arcane.SceneSystem; // For Component
using Arcane.Components; // For Transform
using OpenTK.Mathematics; // For Vector3, Quaternion, MathHelper
using OpenTK.Windowing.GraphicsLibraryFramework; // For MouseButton

namespace TestGame 
{
    public class CameraController : Component
    {
        public float MoveSpeed { get; set; } = 10.0f; // Units per second
        public float MouseSensitivity { get; set; } = 0.1f; // Adjust for comfortable look speed
        public float VerticalSpeed { get; set; } = 5.0f; // Units per second for space/shift
        public float MoveSpeedMultiplier { get; set; } = 2.0f; // Factor to multiply speed by when Ctrl is pressed
        public bool InvertMouseY { get; set; } = false;

        public bool EnableMovementDebugging { get; set; } = true; // Set to false to reduce console spam

        private float _pitch = 0.0f;
        private float _yaw = 0.0f;

        private bool _isMouseLookActive = false;

        private const float MOVEMENT_THRESHOLD_SQUARED = 0.000001f; // Corresponds to a length of 0.001

        public override void OnAwake()
        {
            Input.IsCursorGrabbed = false; // Start with cursor visible

            Vector3 initialEuler = gameObject.transform.localRotation.ToEulerAngles(); // Gets radians
            _pitch = MathHelper.RadiansToDegrees(initialEuler.X);
            _yaw = MathHelper.RadiansToDegrees(initialEuler.Y);
            
            if (EnableMovementDebugging) Debug.Log($"CameraController Awake: Initial Pitch: {_pitch:F2}, Yaw: {_yaw:F2}");
        }

        public override void Update()
        {
            if (gameObject == null || gameObject.transform == null) return;

            // --- Mouse Look Activation/Deactivation ---
            if (!_isMouseLookActive && Input.IsWindowFocused)
            {
                if (Input.GetMouseButtonDown(MouseButton.Left)) 
                {
                    _isMouseLookActive = true;
                    Input.IsCursorGrabbed = true;
                    if (EnableMovementDebugging) Debug.Log("Mouse look ACTIVATED. Cursor grabbed.");
                }
            }
            else if (_isMouseLookActive)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _isMouseLookActive = false;
                    Input.IsCursorGrabbed = false;
                    if (EnableMovementDebugging) Debug.Log("Mouse look DEACTIVATED. Cursor released.");
                }
            }

            // --- Mouse Look Logic ---
            if (_isMouseLookActive && Input.IsWindowFocused)
            {
                Vector2 mouseDelta = Input.MouseDelta;
                // if (EnableMovementDebugging && (mouseDelta.X != 0 || mouseDelta.Y != 0) && _isMouseLookActive) 
                // {
                //     Debug.Log($"Mouse Delta: X={mouseDelta.X:F2}, Y={mouseDelta.Y:F2}");
                // }

                _yaw -= mouseDelta.X * MouseSensitivity; 
                if (InvertMouseY) _pitch += mouseDelta.Y * MouseSensitivity; 
                else _pitch -= mouseDelta.Y * MouseSensitivity;
                _pitch = MathHelper.Clamp(_pitch, -89.0f, 89.0f); 

                Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(_yaw));
                Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(_pitch));
                gameObject.transform.localRotation = yawRotation * pitchRotation;
            }

            // --- Keyboard Movement ---
            if (Input.IsWindowFocused) 
            {
                float deltaTime = (float)Time.deltaTimeSeconds;
                if (deltaTime <= 0) return; 

                float actualMoveSpeed = MoveSpeed;
                float actualVerticalSpeed = VerticalSpeed;

                bool ctrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrlPressed)
                {
                    actualMoveSpeed *= MoveSpeedMultiplier;
                    actualVerticalSpeed *= MoveSpeedMultiplier;
                }

                float currentFrameHorizontalMoveSpeed = actualMoveSpeed * deltaTime;
                float currentFrameVerticalMoveSpeed = actualVerticalSpeed * deltaTime;

                Vector3 finalMoveDirectionWorld = Vector3.Zero;
                Vector3 horizontalMovementInput = Vector3.Zero; 

                bool wPressed = Input.GetKey(KeyCode.W);
                bool sPressed = Input.GetKey(KeyCode.S);
                bool aPressed = Input.GetKey(KeyCode.A);
                bool dPressed = Input.GetKey(KeyCode.D);
                bool spacePressed = Input.GetKey(KeyCode.Space);
                bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                
                if (wPressed) horizontalMovementInput -= Vector3.UnitZ; 
                if (sPressed) horizontalMovementInput += Vector3.UnitZ; 
                if (aPressed) horizontalMovementInput -= Vector3.UnitX; 
                if (dPressed) horizontalMovementInput += Vector3.UnitX; 

                // Calculate world-space horizontal direction based on yaw and input
                if (horizontalMovementInput.LengthSquared > MOVEMENT_THRESHOLD_SQUARED)
                {
                    // Get rotation based purely on yaw (around world Y axis)
                    Quaternion flatYawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(_yaw));
                    // Transform the local input direction (which is on its local XZ plane) by this yaw rotation
                    Vector3 worldDirectionFromInput = Vector3.Transform(horizontalMovementInput.Normalized(), flatYawRotation);
                    
                    // **Explicitly ensure the calculated horizontal direction is on the XZ plane**
                    Vector3 strictlyHorizontalDirection = new Vector3(worldDirectionFromInput.X, 0f, worldDirectionFromInput.Z);
                    
                    // Normalize the strictly horizontal direction to maintain consistent speed,
                    // especially if the projection to XZ somehow altered its intended unit length (it shouldn't significantly if worldDirectionFromInput was already XZ).
                    if (strictlyHorizontalDirection.LengthSquared > MOVEMENT_THRESHOLD_SQUARED) // Check before normalizing to avoid NaN from normalizing a zero vector
                    {
                        finalMoveDirectionWorld += strictlyHorizontalDirection.Normalized() * currentFrameHorizontalMoveSpeed;
                    }
                }

                // Vertical movement (Space/Shift) - directly in world space Y
                if (spacePressed) finalMoveDirectionWorld += Vector3.UnitY * currentFrameVerticalMoveSpeed;
                if (shiftPressed) finalMoveDirectionWorld -= Vector3.UnitY * currentFrameVerticalMoveSpeed;

                // Apply the final movement
                if (finalMoveDirectionWorld.LengthSquared > MOVEMENT_THRESHOLD_SQUARED)
                {
                    gameObject.transform.position += finalMoveDirectionWorld;
                    // if (EnableMovementDebugging && (wPressed || sPressed || aPressed || dPressed || spacePressed || shiftPressed))
                    // {
                    //     Debug.Log($"Moving by: {finalMoveDirectionWorld}. New Pos: {gameObject.transform.position}");
                    // }
                }
            }
            // else if (EnableMovementDebugging && _isMouseLookActive)
            // {
            //     Debug.Log("CameraController: Window not focused, keyboard movement and mouse look (delta) paused.");
            // }
        }

        public override void OnDestroy()
        {
            if (Input.IsCursorGrabbed) 
            {
                Input.IsCursorGrabbed = false;
                if (EnableMovementDebugging) Debug.Log("CameraController OnDestroy. Cursor released.");
            }
        }

        public override void OnEnable()
        {
            if (_isMouseLookActive && Input.IsWindowFocused)
            {
                Input.IsCursorGrabbed = true;
            }
            // if (EnableMovementDebugging) Debug.Log("CameraController OnEnable.");
        }

        public override void OnDisable()
        {
            if (_isMouseLookActive || Input.IsCursorGrabbed) 
            {
                Input.IsCursorGrabbed = false;
                // if (EnableMovementDebugging) Debug.Log("CameraController OnDisable. Cursor released.");
            }
        }
    }
}