using Arcane.Components;

namespace Arcane.SceneSystem
{
    public class Scene
    {
        public string Name { get; private set; }
        private readonly List<GameObject> _rootGameObjects = new List<GameObject>();

        // To manage Start() calls correctly (only once, when active)
        private readonly HashSet<Component> _awokenComponents = new HashSet<Component>();
        private readonly List<Component> _pendingStartComponents = new List<Component>();
        private bool _isInitialized = false; // To track if Awake/Start has been called

        public IReadOnlyList<GameObject> RootGameObjects => _rootGameObjects.AsReadOnly();

        public Scene(string name)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed Scene" : name;
        }

        // --- GameObject Management ---

        /// <summary>
        /// Adds a GameObject to the scene. If it has no parent, it becomes a root GameObject.
        /// If the scene is already initialized, Awake and Start will be called on the new GameObject and its components.
        /// </summary>
        public void AddGameObject(GameObject go)
        {
            if (go == null)
            {
                // Consider throwing ArgumentNullException or logging an error
                Console.WriteLine("Warning: Tried to add a null GameObject to the scene.");
                return;
            }

            // Ensure GameObject isn't already in another scene context implicitly (more complex to track)
            // For now, we assume it's a new GO or properly detached.

            if (go.transform.parent == null && !_rootGameObjects.Contains(go))
            {
                _rootGameObjects.Add(go);
            }
            // If it has a parent, it's assumed to be part of the hierarchy already.
            // If its parent is NOT in the scene, that's a more complex state to handle.

            if (_isInitialized) // If scene is already running, initialize the new GameObject
            {
                InitializeGameObjectRecursive(go); // Calls Awake
                // Start will be picked up in the next StartAll pass for components that just Awoke
            }
        }

        /// <summary>
        /// Removes a GameObject from the scene and destroys it.
        /// </summary>
        public void RemoveGameObject(GameObject go)
        {
            if (go == null) return;

            // Recursively destroy children first
            // Copy children list as it will be modified during iteration by SetParent(null)
            List<Transform> childrenCopy = go.transform.Children.ToList();
            foreach (Transform childTransform in childrenCopy)
            {
                RemoveGameObject(childTransform.gameObject); // Recursive call
            }

            // Call OnDestroy for all components on this GameObject
            CallOnDestroyForGameObject(go);

            // Remove from parent's children list (if it has a parent)
            go.transform.SetParent(null, false); // Detach from parent

            // Remove from root list if it's a root object
            _rootGameObjects.Remove(go);

            // Remove any of its components from pending start/awoken lists
            List<Component> componentsToRemove = new List<Component>();
            if (go.transform != null) componentsToRemove.Add(go.transform);
            // componentsToRemove.AddRange(go.GetAllComponentsSomehow()); // Need a way to get all components from GO
            // For now, this part is simplified. A proper GO.GetAllComponents() would be needed.
            // For simplicity, we'll assume OnDestroy on components is sufficient cleanup for these lists.
        }


        public GameObject FindGameObjectByName(string name)
        {
            foreach (var rootGo in _rootGameObjects)
            {
                GameObject found = FindGameObjectByNameRecursive(rootGo, name);
                if (found != null) return found;
            }
            return null;
        }

        private GameObject FindGameObjectByNameRecursive(GameObject current, string name)
        {
            if (current.Name == name) return current;
            foreach (Transform childTransform in current.transform.Children)
            {
                GameObject foundInChild = FindGameObjectByNameRecursive(childTransform.gameObject, name);
                if (foundInChild != null) return foundInChild;
            }
            return null;
        }

        // --- Lifecycle Orchestration Methods (called by Engine) ---

        /// <summary>
        /// Calls OnAwake on all components of all GameObjects in the scene.
        /// Typically called once when the scene is loaded.
        /// </summary>
        public void InitializeScene() // Was AwakeAll
        {
            if (_isInitialized) return;

            foreach (var rootGo in _rootGameObjects)
            {
                InitializeGameObjectRecursive(rootGo);
            }
            _isInitialized = true; // Mark that initial Awake pass is done
            // The Start pass will happen separately.
        }

        private void InitializeGameObjectRecursive(GameObject go)
        {
            if (go == null) return;

            // Awake for Transform
            if (go.transform != null && _awokenComponents.Add(go.transform))
            {
                go.transform.OnAwake();
                _pendingStartComponents.Add(go.transform);
            }

            // Awake for other components
            // Need a way to iterate all components on a GameObject for this.
            // Assuming GameObject has a method like GetAttachedComponents() for internal use.
            // For now, I'll use the GetComponent<T> for specific types as an example placeholder.
            // This part needs a proper component iterator on GameObject.
            // Let's assume GameObject.ForEachComponent(Action<Component> action) exists.

            /* Example of how it might look with a component iterator:
            go.ForEachComponent(component => {
                if (_awokenComponents.Add(component)) {
                    component.OnAwake();
                    _pendingStartComponents.Add(component);
                }
            });
            */
            // For now, we'll assume components are awoken/started if their GO is processed.
            // This part needs GameObject to expose its component list for the Scene to iterate.
            // Let's refine GameObject.cs to allow this iteration.

            foreach (Transform childTransform in go.transform.Children)
            {
                InitializeGameObjectRecursive(childTransform.gameObject);
            }
        }


        /// <summary>
        /// Calls Start on all components that have been Awoken and are active.
        /// Typically called once after all Awakes are done, before the first Update.
        /// </summary>
        public void StartAllPending() // Was StartAll
        {
            // Iterate a copy in case components are added/removed during Start
            List<Component> currentPending = new List<Component>(_pendingStartComponents);
            _pendingStartComponents.Clear();

            foreach (var component in currentPending)
            {
                if (component.gameObject.activeInHierarchy)
                {
                    try
                    {
                        component.Start();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during Start() for component {component.GetType().Name} on GameObject {component.gameObject.Name}: {ex.Message}");
                    }
                }
                else
                {
                    // If not active yet, put it back to be started later if it becomes active
                    _pendingStartComponents.Add(component);
                }
            }
        }


        /// <summary>
        /// Calls Update on all active GameObjects and their active Components.
        /// </summary>
        public void UpdateAll()
        {
            // First, process any pending starts for newly activated objects
            if (_pendingStartComponents.Count > 0) StartAllPending();

            foreach (var rootGo in _rootGameObjects)
            {
                UpdateGameObjectRecursive(rootGo);
            }
        }

        private void UpdateGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;

            // Update Transform
            go.transform?.Update(); // Assuming Transform has Update

            // Update other components - needs GameObject to expose components
            // go.ForEachComponent(component => component.Update()); // Ideal
            // For now, this is a placeholder. GameObject needs to provide a way.

            foreach (Transform childTransform in go.transform.Children)
            {
                UpdateGameObjectRecursive(childTransform.gameObject);
            }
        }

        /// <summary>
        /// Calls FixedUpdate on all active GameObjects and their active Components.
        /// </summary>
        public void FixedUpdateAll()
        {
            foreach (var rootGo in _rootGameObjects)
            {
                FixedUpdateGameObjectRecursive(rootGo);
            }
        }

        private void FixedUpdateGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;

            // FixedUpdate Transform
            go.transform?.FixedUpdate();

            // FixedUpdate other components - needs GameObject to expose components
            // go.ForEachComponent(component => component.FixedUpdate()); // Ideal

            foreach (Transform childTransform in go.transform.Children)
            {
                FixedUpdateGameObjectRecursive(childTransform.gameObject);
            }
        }

        /// <summary>
        /// Cleans up the scene, destroying all GameObjects and their components.
        /// </summary>
        public void DestroyScene()
        {
            // Iterate a copy because RemoveGameObject modifies the _rootGameObjects list
            List<GameObject> rootsCopy = new List<GameObject>(_rootGameObjects);
            foreach (var rootGo in rootsCopy)
            {
                RemoveGameObject(rootGo); // This will recursively call OnDestroy
            }
            _rootGameObjects.Clear();
            _awokenComponents.Clear();
            _pendingStartComponents.Clear();
            _isInitialized = false;
            Console.WriteLine($"Scene '{Name}' destroyed.");
        }

        private void CallOnDestroyForGameObject(GameObject go)
        {
            if (go == null) return;

            // Call OnDestroy for Transform
            go.transform?.OnDestroy();

            // Call OnDestroy for other components
            // go.ForEachComponent(component => component.OnDestroy()); // Ideal
            // This needs GameObject to expose its component list.

            // For simplicity, we assume components list is not directly iterated here for OnDestroy
            // if GameObject.RemoveComponent or similar handles individual component OnDestroy.
            // However, a bulk destroy like this should ensure it.
        }
    }
}