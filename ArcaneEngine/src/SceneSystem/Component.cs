// Arcane/SceneSystem/Component.cs (Ensure this or similar exists)
namespace Arcane.SceneSystem
{
    public abstract class Component
    {
        public GameObject gameObject { get; internal set; } // Set by GameObject when component is added

        public virtual void OnAwake() { /* Default empty implementation */ }
        public virtual void Start() { /* Default empty implementation */ }
        public virtual void Update() { /* Default empty implementation */ }
        public virtual void FixedUpdate() { /* Default empty implementation */ }
        public virtual void OnEnable() { /* Called when GameObject becomes active or component is enabled */ }
        public virtual void OnDisable() { /* Called when GameObject becomes inactive or component is disabled */ }
        public virtual void OnDestroy() { /* Called before component is removed/destroyed */ }
    }
}