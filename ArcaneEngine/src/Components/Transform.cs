// Arcane/Components/Transform.cs
using System;
using System.Collections.Generic; // For List
using System.Linq; // For AsReadOnly()
using Arcane.SceneSystem; // For Component base class
using OpenTK.Mathematics; // Using OpenTK's math library

namespace Arcane.Components
{
    public enum Space
    {
        Self,  // Relative to the local coordinate system
        World  // Relative to the world coordinate system
    }

    public class Transform : Component
    {
        // --- Private Fields for Local Space ---
        private Vector3 _localPosition = Vector3.Zero;
        private Quaternion _localRotation = Quaternion.Identity;
        private Vector3 _localScale = Vector3.One;

        // --- Hierarchy ---
        private Transform _parent;
        private readonly List<Transform> _children = new List<Transform>();

        // --- Matrices and Dirty Flagging ---
        private Matrix4 _localToWorldMatrix = Matrix4.Identity;
        private Matrix4 _worldToLocalMatrix = Matrix4.Identity;
        private bool _isDirty = true;

        // --- Public Properties for Local Space ---
        public Vector3 localPosition
        {
            get { return _localPosition; }
            set
            {
                if (_localPosition != value)
                {
                    _localPosition = value;
                    SetDirty();
                }
            }
        }

        public Quaternion localRotation
        {
            get { return _localRotation; }
            set
            {
                if (_localRotation != value)
                {
                    _localRotation = value;
                    SetDirty();
                }
            }
        }

        public Vector3 localScale
        {
            get { return _localScale; }
            set
            {
                if (_localScale != value)
                {
                    _localScale = value;
                    SetDirty();
                }
            }
        }

        public Vector3 localEulerAngles // Pitch (X), Yaw (Y), Roll (Z) in Degrees
        {
            get { return ToEulerAnglesDegrees(_localRotation); }
            set { localRotation = FromEulerAnglesDegrees(value); }
        }

        // --- Public Properties for World Space (Calculated) ---
        public Vector3 position
        {
            get { return GetLocalToWorldMatrix().ExtractTranslation(); } // Use ExtractTranslation()
            set
            {
                if (_parent == null)
                {
                    localPosition = value;
                }
                else
                {
                    // Transform world position to parent's local space
                    localPosition = Vector3.TransformPosition(value, _parent.GetWorldToLocalMatrix());
                }
            }
        }

        public Quaternion rotation
        {
            get
            {
                if (_parent == null)
                {
                    return _localRotation;
                }
                // WorldRotation = ParentWorldRotation * LocalRotation
                return _parent.rotation * _localRotation; // OpenTK Quaternion multiplication is direct
            }
            set
            {
                if (_parent == null)
                {
                    localRotation = value;
                }
                else
                {
                    // LocalRotation = Inverse(ParentWorldRotation) * NewWorldRotation
                    localRotation = _parent.rotation.Inverted() * value;
                }
            }
        }

        public Vector3 eulerAngles // World Euler Angles (Pitch, Yaw, Roll) in Degrees
        {
            get { return ToEulerAnglesDegrees(this.rotation); }
            set { this.rotation = FromEulerAnglesDegrees(value); }
        }

        public Vector3 lossyScale
        {
            get
            {
                return GetLocalToWorldMatrix().ExtractScale();
            }
        }

        // --- Hierarchy Management ---
        public Transform parent
        {
            get { return _parent; }
            set { SetParent(value, false); } // Defaulting worldPositionStays to false for direct set
        }

        public int childCount
        {
            get { return _children.Count; }
        }

        public IReadOnlyList<Transform> Children
        {
            get { return _children.AsReadOnly(); }
        }

        public Transform root
        {
            get
            {
                Transform current = this;
                while (current.parent != null)
                {
                    current = current.parent;
                }
                return current;
            }
        }

        // --- Direction Vectors (World Space, Normalized) ---
        // OpenTK's convention: +Z is forward if no rotation.
        // Vector3.Transform with Quaternion applies the rotation.
        public Vector3 forward
        {
            // To match Unity's +Z forward: transform (0,0,1) by the world rotation.
            // OpenTK's default might be -Z for some camera setups, but for object forward, +Z is common.
            get { return Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, this.rotation)); }
        }
        public Vector3 up
        {
            get { return Vector3.Normalize(Vector3.Transform(Vector3.UnitY, this.rotation)); }
        }
        public Vector3 right
        {
            get { return Vector3.Normalize(Vector3.Transform(Vector3.UnitX, this.rotation)); }
        }

        // --- Matrices ---
        public Matrix4 localToWorldMatrix
        {
            get { return GetLocalToWorldMatrix(); }
        }
        public Matrix4 worldToLocalMatrix
        {
            get { return GetWorldToLocalMatrix(); }
        }

        // --- Constructor ---
        public Transform()
        {
            // Fields are initialized with their default values.
        }

        public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this._localPosition = position;
            this._localRotation = rotation;
            this._localScale = scale;
            SetDirty(); // Ensure matrices are calculated if these are non-default
        }

        // Expects Euler angles for the third Vector3 if provided
        public Transform(params Vector3[] initialValues)
        {
            if (initialValues.Length >= 1) this.localPosition = initialValues[0];
            if (initialValues.Length >= 2) this.localScale = initialValues[1];
            if (initialValues.Length >= 3) this.localEulerAngles = initialValues[2]; // Assumes euler angles
        }

        // --- Public Methods ---
        public void SetParent(Transform newParent, bool worldPositionStays = true)
        {
            if (_parent == newParent) return;

            Matrix4 worldMatrixCache = Matrix4.Identity;
            if (worldPositionStays)
            {
                worldMatrixCache = GetLocalToWorldMatrix();
            }

            if (_parent != null)
            {
                _parent._children.Remove(this);
            }

            _parent = newParent;

            if (_parent != null)
            {
                _parent._children.Add(this);
            }

            if (worldPositionStays)
            {
                if (_parent != null)
                {
                    Matrix4 parentWorldToLocal = _parent.GetWorldToLocalMatrix();
                    Matrix4 newLocalMatrix = worldMatrixCache * parentWorldToLocal; // Order might need to be parentWorldToLocal * worldMatrixCache
                                                                                    // If worldMatrixCache is point P in world, and parentWorldToLocal is M_parent_inv
                                                                                    // P_local = M_parent_inv * P_world. So, parentWorldToLocal * worldMatrixCache seems correct.
                                                                                    // Let's test. If Matrix4 is column major, P_local = M_parent_inv * P_world.
                                                                                    // If row major, P_local = P_world * M_parent_inv. OpenTK matrices are row-major.
                                                                                    // So, newLocalMatrix = worldMatrixCache * parentWorldToLocal; is correct.

                    _localPosition = newLocalMatrix.ExtractTranslation();
                    _localRotation = newLocalMatrix.ExtractRotation(true); // Normalize quaternion
                    _localScale = newLocalMatrix.ExtractScale();
                }
                else // Unparenting, world matrix becomes local matrix
                {
                    _localPosition = worldMatrixCache.ExtractTranslation();
                    _localRotation = worldMatrixCache.ExtractRotation(true);
                    _localScale = worldMatrixCache.ExtractScale();
                }
            }
            SetDirty(); // Always mark dirty after parent change
        }

        public void Translate(Vector3 translation, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self)
            {
                // Translates along the transform's local axes
                // _localRotation * translation rotates 'translation' into world space based on local rotation,
                // then adds it to localPosition. This is effectively adding a world-space offset
                // that was defined in local terms.
                // To move along local axes: localPosition += localRotation * translation (if translation is local-axis delta)
                // Or, transform the translation vector by the local rotation and add to localPosition.
                localPosition += Vector3.Transform(translation, _localRotation);
            }
            else // Space.World
            {
                position += translation; // Uses the world position setter
            }
        }

        public void Rotate(Vector3 eulersDegrees, Space relativeTo = Space.Self)
        {
            Rotate(FromEulerAnglesDegrees(eulersDegrees), relativeTo);
        }

        public void Rotate(Quaternion rotationDelta, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self)
            {
                // Apply rotation locally: NewLocalRot = OldLocalRot * DeltaLocalRot
                localRotation = _localRotation * rotationDelta;
            }
            else // Space.World
            {
                // Apply rotation in world space: NewWorldRot = DeltaWorldRot * OldWorldRot
                this.rotation = rotationDelta * this.rotation; // Uses world rotation setter
            }
        }

        public void RotateAround(Vector3 worldPoint, Vector3 worldAxis, float angleDegrees)
        {
            Quaternion rot = Quaternion.FromAxisAngle(Vector3.Normalize(worldAxis), MathHelper.DegreesToRadians(angleDegrees));
            Vector3 dir = this.position - worldPoint;
            dir = Vector3.Transform(dir, rot); // Use TransformRow for vector by quaternion if appropriate, or Vector3.Transform
            this.position = worldPoint + dir;
            this.rotation = rot * this.rotation; // Also rotate the object itself around its new pivot
        }

        public void LookAt(Transform target, Vector3 worldUp = default)
        {
            if (target == null) return;
            LookAt(target.position, worldUp == default ? Vector3.UnitY : worldUp);
        }

        public void LookAt(Vector3 worldTargetPosition, Vector3 worldUp = default)
        {
            if (worldUp == default) worldUp = Vector3.UnitY;
            // Create a matrix that looks at the target from the current position
            // Matrix4.LookAt creates a VIEW matrix (camera transform).
            // To orient an object, we want its local +Z to point towards target.
            // So, the rotation is effectively the inverse of the view matrix's rotation part.
            Matrix4 lookAtMatrix = Matrix4.LookAt(this.position, worldTargetPosition, worldUp);
            // The rotation part of this view matrix needs to be inverted and then applied.
            // Or, construct the rotation directly.
            this.rotation = lookAtMatrix.ExtractRotation(true).Inverted();
        }

        protected void SetDirty()
        {
            if (!_isDirty)
            {
                _isDirty = true;
                foreach (var child in _children)
                {
                    if (child != null) child.SetDirty();
                }
            }
        }

        private Matrix4 CalculateLocalMatrix()
        {
            // TRS order: Scale, then Rotate, then Translate
            return Matrix4.CreateScale(_localScale) *
                   Matrix4.CreateFromQuaternion(_localRotation) *
                   Matrix4.CreateTranslation(_localPosition);
        }

        private Matrix4 GetLocalToWorldMatrix()
        {
            if (_isDirty)
            {
                if (_parent == null)
                {
                    _localToWorldMatrix = CalculateLocalMatrix();
                }
                else
                {
                    // ChildLocalToWorld = ParentWorld * ChildLocal
                    // OpenTK matrices are row-major. Matrix multiplication order is Mlocal * Mparent.
                    _localToWorldMatrix = CalculateLocalMatrix() * _parent.GetLocalToWorldMatrix();
                }
                _worldToLocalMatrix = _localToWorldMatrix.Inverted(); // Calculate inverse here
                _isDirty = false;
            }
            return _localToWorldMatrix;
        }

        private Matrix4 GetWorldToLocalMatrix()
        {
            if (_isDirty) // Ensure world matrix (and thus its inverse) is up-to-date
            {
                GetLocalToWorldMatrix(); // This calculates both and clears dirty flag
            }
            return _worldToLocalMatrix;
        }

        // Euler Angles: OpenTK's ToEulerAngles returns (Pitch, Yaw, Roll) in Radians.
        // We'll assume our EulerAngles properties are (Pitch, Yaw, Roll) in Degrees.
        public static Vector3 ToEulerAnglesDegrees(Quaternion q)
        {
            Vector3 eulerRad = q.ToEulerAngles(); // Returns X:Pitch, Y:Yaw, Z:Roll in radians
            return new Vector3(
                MathHelper.RadiansToDegrees(eulerRad.X), // Pitch
                MathHelper.RadiansToDegrees(eulerRad.Y), // Yaw
                MathHelper.RadiansToDegrees(eulerRad.Z)  // Roll
            );
        }

        public static Quaternion FromEulerAnglesDegrees(Vector3 eulerAnglesInDegrees) // Expects Pitch, Yaw, Roll
        {
            // Convert degrees to radians for OpenTK's FromEulerAngles
            Vector3 eulerRad = new Vector3(
                MathHelper.DegreesToRadians(eulerAnglesInDegrees.X), // Pitch
                MathHelper.DegreesToRadians(eulerAnglesInDegrees.Y), // Yaw
                MathHelper.DegreesToRadians(eulerAnglesInDegrees.Z)  // Roll
            );
            return Quaternion.FromEulerAngles(eulerRad); // Expects X:Pitch, Y:Yaw, Z:Roll
        }

        // MathHelper methods if not globally available
        // public static class MathHelper {
        //     public static float DegreesToRadians(float degrees) { return degrees * (float)(Math.PI / 180.0); }
        //     public static float RadiansToDegrees(float radians) { return radians * (float)(180.0 / Math.PI); }
        // }

        public Vector3 TransformPoint(Vector3 localPoint)
        {
            return Vector3.TransformPosition(localPoint, GetLocalToWorldMatrix());
        }
        public Vector3 InverseTransformPoint(Vector3 worldPoint)
        {
            return Vector3.TransformPosition(worldPoint, GetWorldToLocalMatrix());
        }
        public Vector3 TransformDirection(Vector3 localDirection)
        {
            // For directions, we don't want translation. Use TransformNormal or the rotation part.
            return Vector3.TransformNormal(localDirection, GetLocalToWorldMatrix());
        }
        public Vector3 InverseTransformDirection(Vector3 worldDirection)
        {
            return Vector3.TransformNormal(worldDirection, GetWorldToLocalMatrix());
        }
    }
}
