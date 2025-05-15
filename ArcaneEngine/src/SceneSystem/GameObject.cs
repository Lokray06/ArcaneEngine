// Arcane/SceneSystem/GameObject.cs
using System; // For ArgumentNullException, etc.
using System.Collections.Generic;
using System.Linq; // For FirstOrDefault, Where, etc.
using Arcane.Components; // Assuming your Transform class is here

namespace Arcane.SceneSystem
{
    public class GameObject
    {
        private string _name;
        private readonly List<Component> _components = new List<Component>();
        private readonly Transform _transform; // Every GameObject has a Transform

        private bool _activeSelf = true; // Is this GameObject locally active?

        // --- Properties ---
        public string Name
        {
            get { return _name; }
            set { _name = string.IsNullOrEmpty(value) ? "Unnamed GameObject" : value; }
        }

        /// <summary>
        /// The Transform attached to this GameObject (Read-Only).
        /// Every GameObject has a Transform.
        /// </summary>
        public Transform transform => _transform; // Shorter C# 6+ syntax for read-only property

        /// <summary>
        /// The local active state of this GameObject.
        /// Use SetActive() to change this.
        /// </summary>
        public bool activeSelf => _activeSelf;

        /// <summary>
        /// Is the GameObject active in the scene?
        /// This is true if activeSelf is true and all its parents are activeInHierarchy.
        /// </summary>
        public bool activeInHierarchy
        {
            get
            {
                if (!_activeSelf) return false;
                if (_transform.parent == null) return _activeSelf; // No parent, just depends on its own activeSelf
                return _activeSelf && _transform.parent.gameObject.activeInHierarchy; // Recursively checks parent
            }
        }

        // --- Constructors ---
        public GameObject(string name = "GameObject")
        {
            Name = name; // Use the property to ensure validation
            _components = new List<Component>();

            // Every GameObject must have a Transform. Add it by default.
            _transform = AddComponentInternal<Transform>();
            // The line `private Transform _transform = new Transform();` in your original
            // is also fine, but we need to ensure its `gameObject` is set.
            // Let's refine this:
        }

        // Corrected constructor for better Transform initialization
        public GameObject(string name, params Type[] componentTypes) : this(name) // Calls the base constructor
        {
            foreach (var type in componentTypes)
            {
                if (typeof(Component).IsAssignableFrom(type))
                {
                    // This is a simplified AddComponent by Type, assumes new() constraint if used.
                    // For now, let's assume these are just for example and primary way is AddComponent<T>
                    if (type == typeof(Transform)) continue; // Transform already added

                    Component newComp = (Component)Activator.CreateInstance(type);
                    AddComponent(newComp);
                }
            }
        }


        // --- Component Management ---

        /// <summary>
        /// Adds a component of type T to the GameObject.
        /// </summary>
        /// <typeparam name="T">The type of Component to add.</typeparam>
        /// <returns>The added component.</returns>
        public T AddComponent<T>() where T : Component, new()
        {
            // Prevent adding multiple Transforms
            if (typeof(T) == typeof(Transform) && _transform != null)
            {
                Console.WriteLine($"Warning: GameObject '{Name}' already has a Transform component. Returning existing one.");
                return _transform as T;
            }

            T newComponent = new T();
            newComponent.gameObject = this; // Set the back-reference
            _components.Add(newComponent);

            // Optional: If engine is running and GameObject is active, call OnAwake/OnEnable here
            // if (this.activeInHierarchy && Engine.IsRunningOrInitialized) { newComponent.OnAwake(); newComponent.OnEnable(); }
            // For simplicity, we'll assume lifecycle methods are called by a SceneManager or Engine loop.
            // However, OnAwake is often called immediately upon component addition or instantiation.
            // Let's assume an immediate OnAwake call for now if it hasn't been called.
            // This requires tracking if OnAwake was called or having the SceneManager handle it.
            // For now, we'll keep it simple: just add. The Scene will call OnAwake later.

            return newComponent;
        }

        /// <summary>
        /// Internal helper to add component and ensure gameObject is set, used by constructor for Transform.
        /// </summary>
        private T AddComponentInternal<T>() where T : Component, new()
        {
            T newComponent = new T();
            newComponent.gameObject = this;
            // Note: _components might not be initialized if called before base constructor logic fully completes.
            // This is why direct _transform initialization in field or simple constructor is safer.
            // Let's stick to the initial _transform creation in the field or a dedicated step in constructor.

            // Corrected approach for _transform initialization:
            // The _transform field is initialized first, then its gameObject property is set.
            // This method is more for generic components.

            // The `_transform` is special. It's better to initialize it directly.
            // See the constructor for how it's handled. This comment refers to the original thought process.
            _components.Add(newComponent);
            return newComponent;
        }


        /// <summary>
        /// Adds an existing component instance to the GameObject.
        /// Usually used internally or when creating GameObjects programmatically with pre-made components.
        /// </summary>
        public T AddComponent<T>(T component) where T : Component
        {
            if (component == null)
            {
                // Or throw ArgumentNullException
                Console.WriteLine("Error: Cannot add a null component.");
                return null;
            }
            if (component.gameObject != null && component.gameObject != this)
            {
                // Or throw InvalidOperationException
                Console.WriteLine($"Error: Component is already attached to another GameObject ('{component.gameObject.Name}').");
                return null;
            }
            if (typeof(T) == typeof(Transform) && _transform != null && component != _transform)
            {
                Console.WriteLine($"Warning: GameObject '{Name}' already has a Transform. Cannot add another one.");
                return _transform as T; // Or throw an error
            }

            component.gameObject = this;
            if (!_components.Contains(component))
            {
                _components.Add(component);
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
                return _transform as T; // Optimization and correctness for Transform
            }

            foreach (Component component in _components)
            {
                if (component is T typedComponent)
                {
                    return typedComponent;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all components of type T attached to this GameObject.
        /// </summary>
        /// <typeparam name="T">The type of Component to retrieve.</typeparam>
        /// <returns>A list of components of type T. The list is a new copy.</returns>
        public List<T> GetComponents<T>() where T : Component
        {
            List<T> foundComponents = new List<T>();
            if (typeof(T) == typeof(Transform) && _transform != null)
            {
                foundComponents.Add(_transform as T);
                // Typically a GameObject only has one Transform, so we might stop here
                // or continue if for some reason other Transform-derived components could exist.
                // For strict "only one transform" rule, we'd just return this list.
                return foundComponents;
            }

            foreach (Component component in _components)
            {
                if (component is T typedComponent)
                {
                    foundComponents.Add(typedComponent);
                }
            }
            return foundComponents;
        }

        // GetComponentInChildren, GetComponentsInChildren, RemoveComponent would be added similarly.
        // For brevity, I'll skip their full implementation here, but the pattern is:
        // - GetComponentInChildren: Check self, then recursively check children's transforms.
        // - RemoveComponent: Find the component, call OnDestroy if it exists, remove from list.

        // In Arcane/SceneSystem/GameObject.cs

        // Make sure you have this field for your main transform:
        // private readonly Transform _transformComponent; 
        // And the public property:
        // public Transform transform => _transformComponent;

        /// <summary>
        /// Sets the local active state of the GameObject.
        /// This can affect the activeInHierarchy state of this GameObject and all its children.
        /// OnEnable and OnDisable will be called on this GameObject's components if its
        /// activeInHierarchy state changes as a result of this call.
        /// </summary>
        /// <param name="value">True to activate, false to deactivate.</param>
        public void SetActive(bool value)
        {
            if (_activeSelf == value) return; // No change in local state, so no change in hierarchy state from this call

            // Determine the activeInHierarchy state *before* changing _activeSelf
            // This is crucial to detect if this specific SetActive call causes a transition.
            bool wasActiveInHierarchy = this.activeInHierarchy;

            _activeSelf = value; // Apply the local state change

            // Determine the activeInHierarchy state *after* changing _activeSelf
            bool isNowActiveInHierarchy = this.activeInHierarchy;

            // Only trigger OnEnable/OnDisable if the overall activeInHierarchy state of this GameObject changed.
            if (wasActiveInHierarchy != isNowActiveInHierarchy)
            {
                if (isNowActiveInHierarchy) // Transitioned from Inactive (in hierarchy) to Active (in hierarchy)
                {
                    // Call OnEnable on the Transform component itself
                    if (this.transform != null) // 'transform' is the public property
                    {
                        this.transform.OnEnable();
                    }
                    // Call OnEnable on all other components in the list
                    foreach (var component in _components)
                    {
                        component.OnEnable();
                    }

                    // TODO: Propagate activation to children.
                    // Children that are activeSelf should now also check if their activeInHierarchy state changed.
                    // This typically involves recursively calling a similar logic or having them subscribe to parent changes.
                    // For a simpler immediate effect, you could iterate children and call an "UpdateActiveStatus" method on them.
                    // Example:
                    // foreach(Transform childT in this.transform.Children) { childT.gameObject.HandleParentActivationChange(true); }

                }
                else // Transitioned from Active (in hierarchy) to Inactive (in hierarchy)
                {
                    // Call OnDisable on the Transform component itself
                    if (this.transform != null)
                    {
                        this.transform.OnDisable();
                    }
                    // Call OnDisable on all other components in the list
                    foreach (var component in _components)
                    {
                        component.OnDisable();
                    }

                    // TODO: Propagate deactivation to children.
                    // All children are now effectively inactive in the hierarchy.
                    // Example:
                    // foreach(Transform childT in this.transform.Children) { childT.gameObject.HandleParentActivationChange(false); }
                }
            }
            // Note: If activeInHierarchy didn't change (e.g., a parent is inactive, so this GO remains inactiveInHierarchy
            // regardless of its _activeSelf), then OnEnable/OnDisable for *this* object's components are NOT called here.
            // They would be called if the PARENT's SetActive call caused this object to transition.
        }

        // Initialization for the _transform field, ensuring its gameObject is set.
        // This replaces the direct initializer `private Transform _transform = new Transform();`
        // to ensure `gameObject` is set on the Transform.
        // The constructor now handles this:
        public GameObject() : this("GameObject") // Default constructor calls the one with name
        {
            // The primary constructor now handles transform creation.
            // We need to ensure _transform is non-null.
            // The constructor `public GameObject(string name = "GameObject")` in this version
            // needs to ensure _transform is properly initialized and its gameObject is set.

            // Let's refine constructors again for clarity on Transform initialization.
        }
    }
}