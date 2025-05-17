// Arcane/SceneSystem/Scene.cs
using Arcane.Components;
using System; // For Exception
using System.Collections.Generic; // For List, HashSet
using System.Linq; // For ToList

namespace Arcane.SceneSystem
{
    public class Scene
    {
        public string Name { get; private set; }
        private readonly List<GameObject> rootGameObjects = new List<GameObject>();

        private readonly HashSet<Component> awokenComponents = new HashSet<Component>();
        private readonly List<Component> pendingStartComponents = new List<Component>();
        private bool isInitialized = false;

        public IReadOnlyList<GameObject> RootGameObjects => rootGameObjects.AsReadOnly();

        public Scene(string name)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed Scene" : name;
        }

        public void AddGameObject(GameObject go)
        {
            if (go == null)
            {
                Arcane.Core.Debug.LogWarning($"Scene '{Name}': Tried to add a null GameObject.");
                return;
            }

            if (go.transform.parent == null && !rootGameObjects.Contains(go))
            {
                rootGameObjects.Add(go);
            }

            if (isInitialized && go.activeInHierarchy) // Only initialize if GO is active
            {
                InitializeGameObjectRecursive(go); // Calls Awake
                // StartAllPending will be called before the next Update pass
            }
        }

        public void RemoveGameObject(GameObject go) // Renamed from DestroyGameObject for clarity
        {
            if (go == null) return;

            // Recursively remove children first by creating a copy for safe iteration
            List<Transform> childrenCopy = go.transform.Children.ToList();
            foreach (Transform childTransform in childrenCopy)
            {
                RemoveGameObject(childTransform.gameObject);
            }

            // Call OnDestroy for all components on this GameObject
            CallOnDestroyForGameObject(go);

            // Remove from parent's children list
            go.transform.SetParent(null, false);

            // Remove from root list
            rootGameObjects.Remove(go);

            // Clean up component references from scene's tracking lists
            foreach (var component in go.GetAllComponents()) // Iterate using the new method
            {
                awokenComponents.Remove(component);
                pendingStartComponents.Remove(component);
            }
        }

        public GameObject FindGameObjectByName(string name)
        {
            foreach (var rootGo in rootGameObjects)
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

        public void InitializeScene()
        {
            if (isInitialized) return;

            // Create a copy for safe iteration if GameObjects are added/removed during Awake
            List<GameObject> currentRoots = rootGameObjects.ToList();
            foreach (var rootGo in currentRoots)
            {
                if (rootGo.activeInHierarchy) // Only Awake active GameObjects
                {
                    InitializeGameObjectRecursive(rootGo);
                }
            }
            isInitialized = true;
        }

        private void InitializeGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return; // Process only active GameObjects

            foreach (Component component in go.GetAllComponents()) // Use the new method
            {
                if (awokenComponents.Add(component)) // If successfully added (wasn't already awoken)
                {
                    try
                    {
                        component.OnAwake();
                        // Add to pending start only if the component's GO is active and component is enabled (implicitly true for now)
                        if (component.gameObject.activeInHierarchy) // Double check, though parent call implies it
                        {
                            pendingStartComponents.Add(component);
                        }
                    }
                    catch (Exception ex)
                    {
                        Arcane.Core.Debug.LogError($"Error during OnAwake() for {component.GetType().Name} on {go.Name}: {ex.Message}");
                    }
                }
            }

            // Recursively initialize children that are active
            foreach (Transform childTransform in go.transform.Children)
            {
                if (childTransform.gameObject.activeInHierarchy)
                {
                    InitializeGameObjectRecursive(childTransform.gameObject);
                }
            }
        }

        public void StartAllPending()
        {
            if (pendingStartComponents.Count == 0) return;

            // Iterate a copy in case components are added/removed during Start
            // or if their active state changes.
            List<Component> currentPending = pendingStartComponents.ToList();
            pendingStartComponents.Clear(); // Clear original list before processing

            foreach (var component in currentPending)
            {
                // Ensure the component and its GameObject are still valid and active before calling Start
                if (component != null && component.gameObject != null && component.gameObject.activeInHierarchy)
                {
                    try
                    {
                        component.Start();
                    }
                    catch (Exception ex)
                    {
                        Arcane.Core.Debug.LogError($"Error during Start() for {component.GetType().Name} on {component.gameObject.Name}: {ex.Message}");
                    }
                }
                else if (component != null && component.gameObject != null && !component.gameObject.activeInHierarchy)
                {
                    // If it became inactive before Start, put it back to be started later if it becomes active again.
                    // This can happen if SetActive(false) was called after Awake but before Start.
                    pendingStartComponents.Add(component);
                }
            }
        }

        public void UpdateAll()
        {
            if (pendingStartComponents.Count > 0) StartAllPending();

            // Iterate a copy for safety if GameObjects/components are modified during Update
            List<GameObject> currentRoots = rootGameObjects.ToList();
            foreach (var rootGo in currentRoots)
            {
                UpdateGameObjectRecursive(rootGo);
            }
        }

        private void UpdateGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;

            // Iterate over a copy of components if components can be added/removed during Update
            // For simplicity, direct iteration is used here. Consider implications.
            foreach (Component component in go.GetAllComponents()) // Use the new method
            {
                try
                {
                    component.Update();
                }
                catch (Exception ex)
                {
                    Arcane.Core.Debug.LogError($"Error during Update() for {component.GetType().Name} on {go.Name}: {ex.Message}");
                }
            }

            List<Transform> childrenCopy = go.transform.Children.ToList(); // Iterate copy for safety
            foreach (Transform childTransform in childrenCopy)
            {
                UpdateGameObjectRecursive(childTransform.gameObject);
            }
        }

        public void FixedUpdateAll()
        {
            // Iterate a copy for safety
            List<GameObject> currentRoots = rootGameObjects.ToList();
            foreach (var rootGo in currentRoots)
            {
                FixedUpdateGameObjectRecursive(rootGo);
            }
        }

        private void FixedUpdateGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;

            foreach (Component component in go.GetAllComponents()) // Use the new method
            {
                try
                {
                    component.FixedUpdate();
                }
                catch (Exception ex)
                {
                    Arcane.Core.Debug.LogError($"Error during FixedUpdate() for {component.GetType().Name} on {go.Name}: {ex.Message}");
                }
            }

            List<Transform> childrenCopy = go.transform.Children.ToList(); // Iterate copy for safety
            foreach (Transform childTransform in childrenCopy)
            {
                FixedUpdateGameObjectRecursive(childTransform.gameObject);
            }
        }

        public void DestroyScene()
        {
            Arcane.Core.Debug.Log($"Scene '{Name}': Starting destruction...");
            List<GameObject> rootsCopy = rootGameObjects.ToList();
            foreach (var rootGo in rootsCopy)
            {
                RemoveGameObject(rootGo); // This will recursively call OnDestroy
            }
            rootGameObjects.Clear();
            awokenComponents.Clear();
            pendingStartComponents.Clear();
            isInitialized = false;
            Arcane.Core.Debug.Log($"Scene '{Name}' destroyed.");
        }

        private void CallOnDestroyForGameObject(GameObject go)
        {
            if (go == null) return;
            Arcane.Core.Debug.Log($"Scene '{Name}': Calling OnDestroy for components of GameObject '{go.Name}'.");

            // Call OnDestroy in reverse order of addition might be safer for dependencies,
            // but direct order is simpler for now. Transform is usually first.
            // Create a copy for iteration as components might try to remove themselves or others.
            List<Component> componentsCopy = go.GetAllComponents().ToList();
            foreach (Component component in componentsCopy)
            {
                try
                {
                    component.OnDestroy();
                }
                catch (Exception ex)
                {
                    Arcane.Core.Debug.LogError($"Error during OnDestroy() for {component.GetType().Name} on {go.Name}: {ex.Message}");
                }
            }
        }
    }
}
