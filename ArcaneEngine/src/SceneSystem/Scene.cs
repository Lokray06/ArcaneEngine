// Arcane/SceneSystem/Scene.cs
using Arcane.Components;
using System; // For Exception
using System.Collections.Generic; // For List, HashSet
using System.Linq; // For ToList
using Arcane.Rendering; // Added for Skybox

namespace Arcane.SceneSystem
{
    public class Scene
    {
        public string Name { get; private set; }
        private readonly List<GameObject> rootGameObjects = new List<GameObject>();

        private readonly HashSet<Component> awokenComponents = new HashSet<Component>();
        private readonly List<Component> pendingStartComponents = new List<Component>();
        private bool isInitialized = false;

        public Skybox Skybox { get; set; } // Added Skybox property

        public IReadOnlyList<GameObject> RootGameObjects => rootGameObjects.AsReadOnly();

        public Scene(string name)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed Scene" : name;
            Skybox = null; // Initialize Skybox as null
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

            if (isInitialized && go.activeInHierarchy) 
            {
                InitializeGameObjectRecursive(go); 
            }
        }

        public void RemoveGameObject(GameObject go) 
        {
            if (go == null) return;

            List<Transform> childrenCopy = go.transform.Children.ToList();
            foreach (Transform childTransform in childrenCopy)
            {
                RemoveGameObject(childTransform.gameObject);
            }

            CallOnDestroyForGameObject(go);
            go.transform.SetParent(null, false);
            rootGameObjects.Remove(go);

            foreach (var component in go.GetAllComponents()) 
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

            List<GameObject> currentRoots = rootGameObjects.ToList();
            foreach (var rootGo in currentRoots)
            {
                if (rootGo.activeInHierarchy) 
                {
                    InitializeGameObjectRecursive(rootGo);
                }
            }
            isInitialized = true;
        }

        private void InitializeGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return; 

            foreach (Component component in go.GetAllComponents()) 
            {
                if (awokenComponents.Add(component)) 
                {
                    try
                    {
                        component.OnAwake();
                        if (component.gameObject.activeInHierarchy) 
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

            List<Component> currentPending = pendingStartComponents.ToList();
            pendingStartComponents.Clear(); 

            foreach (var component in currentPending)
            {
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
                    pendingStartComponents.Add(component);
                }
            }
        }

        public void UpdateAll()
        {
            if (pendingStartComponents.Count > 0) StartAllPending();

            List<GameObject> currentRoots = rootGameObjects.ToList();
            foreach (var rootGo in currentRoots)
            {
                UpdateGameObjectRecursive(rootGo);
            }
        }

        private void UpdateGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;

            foreach (Component component in go.GetAllComponents()) 
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

            List<Transform> childrenCopy = go.transform.Children.ToList(); 
            foreach (Transform childTransform in childrenCopy)
            {
                UpdateGameObjectRecursive(childTransform.gameObject);
            }
        }

        public void FixedUpdateAll()
        {
            List<GameObject> currentRoots = rootGameObjects.ToList();
            foreach (var rootGo in currentRoots)
            {
                FixedUpdateGameObjectRecursive(rootGo);
            }
        }

        private void FixedUpdateGameObjectRecursive(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;

            foreach (Component component in go.GetAllComponents()) 
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

            List<Transform> childrenCopy = go.transform.Children.ToList(); 
            foreach (Transform childTransform in childrenCopy)
            {
                FixedUpdateGameObjectRecursive(childTransform.gameObject);
            }
        }

        public void DestroyScene()
        {
            Arcane.Core.Debug.Log($"Scene '{Name}': Starting destruction...");
            
            // Dispose the scene's skybox if it exists
            Skybox?.Dispose();
            Skybox = null;
            Arcane.Core.Debug.Log($"Scene '{Name}': Skybox disposed.");

            List<GameObject> rootsCopy = rootGameObjects.ToList();
            foreach (var rootGo in rootsCopy)
            {
                RemoveGameObject(rootGo); 
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
            // Arcane.Core.Debug.Log($"Scene '{Name}': Calling OnDestroy for components of GameObject '{go.Name}'."); // Can be too verbose

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