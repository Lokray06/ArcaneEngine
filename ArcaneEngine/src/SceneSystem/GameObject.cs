// Arcane/SceneSystem/GameObject.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Arcane.Components; // For Transform

namespace Arcane.SceneSystem
{
    public class GameObject
    {
        private string _name;
        private readonly List<Component> _components = new List<Component>();
        private readonly Transform _transform; // Every GameObject has a Transform

        private bool _activeSelf = true;

        public string Name
        {
            get { return _name; }
            set { _name = string.IsNullOrEmpty(value) ? "Unnamed GameObject" : value; }
        }

        public Transform transform => _transform;

        public bool activeSelf => _activeSelf;

        public bool activeInHierarchy
        {
            get
            {
                if (!_activeSelf) return false;
                if (_transform.parent == null) return _activeSelf; // No parent, just depends on its own activeSelf
                return _activeSelf && _transform.parent.gameObject.activeInHierarchy; // Recursively checks parent
            }
        }

        // Constructor
        public GameObject(string name = "GameObject")
        {
            Name = name;
            // Initialize the Transform component first and add it to the list.
            // The Transform's gameObject property is set here.
            _transform = new Transform { gameObject = this }; // Ensure gameObject is set immediately
            _components.Add(_transform); // Add transform to the component list
        }

        public GameObject(string name, params Type[] componentTypes) : this(name) // Calls the base constructor
        {
            foreach (var type in componentTypes)
            {
                if (typeof(Component).IsAssignableFrom(type))
                {
                    if (type == typeof(Transform)) continue; // Transform already added by the base constructor

                    Component newComp = (Component)Activator.CreateInstance(type);
                    AddComponent(newComp); // Use the instance-adding AddComponent
                }
            }
        }

        // --- Component Management ---

        /// <summary>
        /// Adds a new component of type T to the GameObject.
        /// </summary>
        /// <typeparam name="T">The type of Component to add. Must have a new() constraint.</typeparam>
        /// <returns>The added component, or the existing Transform if T is Transform.</returns>
        public T AddComponent<T>() where T : Component, new()
        {
            if (typeof(T) == typeof(Transform))
            {
                // Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Attempted to add another Transform. Returning existing one.");
                return _transform as T;
            }

            // Optional: Check if a component of the same type already exists if you want to disallow duplicates.
            // Example:
            // if (_components.Any(c => c.GetType() == typeof(T)))
            // {
            //     Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Component of type {typeof(T).Name} already exists. Returning existing one.");
            //     return _components.First(c => c.GetType() == typeof(T)) as T;
            // }

            T newComponent = new T();
            newComponent.gameObject = this; // Set the back-reference
            _components.Add(newComponent);

            // The Scene system (Scene.cs and SceneManager.cs) is responsible for calling
            // OnAwake and Start at the appropriate times (e.g., when GameObject is added to an active scene
            // or during scene initialization).

            return newComponent;
        }

        /// <summary>
        /// Adds an existing component instance to the GameObject.
        /// </summary>
        /// <typeparam name="T">The type of the component.</typeparam>
        /// <param name="component">The component instance to add.</param>
        /// <returns>The added component, or null/existing if issues occur.</returns>
        public T AddComponent<T>(T component) where T : Component
        {
            if (component == null)
            {
                Arcane.Core.Debug.LogError($"GameObject '{Name}': Cannot add a null component.");
                return null;
            }
            if (component.gameObject != null && component.gameObject != this)
            {
                Arcane.Core.Debug.LogError($"GameObject '{Name}': Component of type {component.GetType().Name} is already attached to another GameObject ('{component.gameObject.Name}').");
                return null;
            }
            if (component is Transform && component != _transform) // Check if it's a Transform instance but not THE transform
            {
                Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Attempted to add another Transform instance. Use the existing 'transform' property.");
                return _transform as T; // Return the existing transform
            }

            component.gameObject = this; // Ensure back-reference is set
            if (!_components.Contains(component))
            {
                _components.Add(component);
            }
            else if (component != _transform) // Avoid warning if the transform was re-added (though constructor handles it)
            {
                Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Component instance of type {component.GetType().Name} is already present.");
            }
            return component;
        }

        /// <summary>
        /// Gets the first component of type T attached to this GameObject.
        /// </summary>
        /// <typeparam name="T">The type of Component to retrieve.</typeparam>
        /// <returns>The component of type T, or null if none is found.</returns>
        public T GetComponent<T>() where T : Component
        {
            if (typeof(T) == typeof(Transform))
            {
                return _transform as T; // Optimization for the common case
            }
            // Iterate through components to find the first matching type.
            // Using Linq's OfType<T>().FirstOrDefault() is clean and efficient enough for most cases.
            return _components.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Gets all components of type T attached to this GameObject.
        /// </summary>
        /// <typeparam name="T">The type of Component to retrieve.</typeparam>
        /// <returns>A new list of components of type T. Returns an empty list if none are found.</returns>
        public List<T> GetComponents<T>() where T : Component
        {
            // Using Linq's OfType<T>().ToList() to filter and create a new list.
            return _components.OfType<T>().ToList();
        }

        /// <summary>
        /// Gets an enumerable collection of all components attached to this GameObject,
        /// including the Transform. This is primarily for internal use by the Scene system
        /// to iterate over components for lifecycle method calls (Awake, Start, Update, etc.).
        /// </summary>
        /// <returns>An enumerable of all components.</returns>
        internal IEnumerable<Component> GetAllComponents()
        {
            // Returning _components directly as IEnumerable is fine for foreach loops.
            // If external modification is a concern and this were public,
            // returning _components.AsReadOnly() or a new list might be considered.
            return _components;
        }

        /// <summary>
        /// Removes the first component of type T found on this GameObject.
        /// The Transform component cannot be removed.
        /// </summary>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        public void RemoveComponent<T>() where T : Component
        {
            if (typeof(T) == typeof(Transform))
            {
                Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Cannot remove the Transform component.");
                return;
            }

            T componentToRemove = GetComponent<T>(); // Find the component
            if (componentToRemove != null)
            {
                componentToRemove.OnDestroy(); // Call its OnDestroy lifecycle method
                _components.Remove(componentToRemove); // Remove from the list
            }
        }

        /// <summary>
        /// Removes the specified component instance from this GameObject.
        /// The Transform component cannot be removed.
        /// </summary>
        /// <param name="component">The component instance to remove.</param>
        public void RemoveComponent(Component component)
        {
            if (component == null)
            {
                Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Attempted to remove a null component.");
                return;
            }
            if (component == _transform)
            {
                Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Cannot remove the Transform component.");
                return;
            }

            if (_components.Contains(component))
            {
                component.OnDestroy(); // Call its OnDestroy lifecycle method
                _components.Remove(component); // Remove from the list
            }
            else
            {
                Arcane.Core.Debug.LogWarning($"GameObject '{Name}': Attempted to remove component of type {component.GetType().Name} that is not attached.");
            }
        }

        /// <summary>
        /// Sets the local active state of the GameObject.
        /// This will also trigger OnEnable/OnDisable on its components and propagate
        /// the active state change to its children if its activeInHierarchy state changes.
        /// </summary>
        /// <param name="value">True to activate, false to deactivate.</param>
        public void SetActive(bool value)
        {
            if (_activeSelf == value) return; // No change in local state

            bool wasActiveInHierarchy = this.activeInHierarchy; // Check state *before* local change
            _activeSelf = value; // Apply local change
            bool isNowActiveInHierarchy = this.activeInHierarchy; // Check state *after* local change

            if (wasActiveInHierarchy != isNowActiveInHierarchy) // If the effective hierarchy state changed
            {
                // Call OnEnable or OnDisable on all components of this GameObject
                foreach (var component in _components) // _components includes the Transform
                {
                    if (isNowActiveInHierarchy)
                    {
                        component.OnEnable();
                    }
                    else
                    {
                        component.OnDisable();
                    }
                }

                // Propagate the change to children. Each child will then determine
                // if its own activeInHierarchy state changed.
                foreach (Transform childT in this.transform.Children)
                {
                    childT.gameObject.PropagateActiveStateChange(isNowActiveInHierarchy);
                }
            }
        }

        /// <summary>
        /// Internal method called by a parent GameObject when its activeInHierarchy state changes.
        /// This method determines if the current GameObject's activeInHierarchy state
        /// also changes as a result and triggers OnEnable/OnDisable on its components accordingly,
        /// then propagates the change to its own children.
        /// </summary>
        /// <param name="parentIsNowActiveInHierarchy">The new activeInHierarchy state of the parent.</param>
        internal void PropagateActiveStateChange(bool parentIsNowActiveInHierarchy)
        {
            // The activeInHierarchy getter already considers the parent's state.
            // We need to see if our state *changed* because of the parent's change.
            bool wasActiveInHierarchy = this.activeInHierarchy;

            // To correctly assess the 'new' state, we must consider our _activeSelf
            // in conjunction with the parent's new state.
            // The activeInHierarchy getter already does this.
            bool isNowActiveInHierarchy = _activeSelf && parentIsNowActiveInHierarchy;
            // If this GO is locally inactive (_activeSelf = false), it remains inactive in hierarchy.
            // If it's locally active, its hierarchy status depends on the parent.

            // If this object was active but now its path to root is broken OR it itself is set inactive
            if (wasActiveInHierarchy && !isNowActiveInHierarchy)
            {
                foreach (var component in _components)
                {
                    component.OnDisable();
                }
                // If this GO becomes inactive in hierarchy, all its children also do.
                foreach (Transform childT in this.transform.Children)
                {
                    childT.gameObject.PropagateActiveStateChange(false);
                }
            }
            // If this object was inactive but now its path to root is active AND it itself is active
            else if (!wasActiveInHierarchy && isNowActiveInHierarchy)
            {
                foreach (var component in _components)
                {
                    component.OnEnable();
                }
                // If this GO becomes active in hierarchy, propagate to children.
                // They will decide based on their own _activeSelf.
                foreach (Transform childT in this.transform.Children)
                {
                    childT.gameObject.PropagateActiveStateChange(true);
                }
            }
            // If no change in this object's activeInHierarchy state, do nothing further here.
        }
    }
}
