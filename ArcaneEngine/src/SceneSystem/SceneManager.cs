using Arcane.Components;
using Arcane.Core;

namespace Arcane.SceneSystem
{
    public static class SceneManager
    {
        public static Scene ActiveScene { get; private set; }
        public static GameObject MainCamera { get; private set; }

        public static void LoadScene(Scene scene)
        {
            if (scene == null)
            {
                Debug.LogError("SceneManager: Cannot load a null scene.");
                return;
            }

            Debug.Log($"SceneManager: Unloading current scene ('{ActiveScene?.Name ?? "None"}')...");
            ActiveScene?.DestroyScene(); // Clean up the old scene

            Debug.Log($"SceneManager: Loading scene '{scene.Name}'...");
            ActiveScene = scene;
            ActiveScene.InitializeScene(); // Calls OnAwake
            ActiveScene.StartAllPending(); // Calls Start

            FindMainCamera(); // Find camera after scene is initialized
            Debug.Log($"SceneManager: Scene '{ActiveScene.Name}' loaded. Main camera: '{MainCamera?.Name ?? "None"}'.");
        }

        public static void UpdateCurrentScene()
        {
            ActiveScene?.UpdateAll();
        }

        public static void FixedUpdateCurrentScene()
        {
            ActiveScene?.FixedUpdateAll();
        }

        public static void DestroyCurrentScene()
        {
            ActiveScene?.DestroyScene();
            ActiveScene = null;
            MainCamera = null;
            Debug.Log("SceneManager: Current scene destroyed.");
        }

        public static void FindMainCamera()
        {
            if (ActiveScene == null)
            {
                MainCamera = null;
                return;
            }

            // Simple strategy: find the first active GameObject with a CameraComponent
            // More advanced: find a "MainCamera" tag, or a designated primary camera.
            foreach (var rootGo in ActiveScene.RootGameObjects)
            {
                MainCamera = FindCameraRecursive(rootGo);
                if (MainCamera != null) break;
            }

            if (MainCamera == null)
            {
                Debug.LogWarning("SceneManager: No active camera found in the scene.");
            }
            // Aspect ratio update for the camera should happen in the Engine or Renderer
            // when the window size is known or changes.
        }

        private static GameObject FindCameraRecursive(GameObject currentGo)
        {
            if (currentGo == null || !currentGo.activeInHierarchy) return null;

            if (currentGo.GetComponent<CameraComponent>() != null)
            {
                return currentGo;
            }
            foreach (var childTransform in currentGo.transform.Children)
            {
                GameObject foundInChild = FindCameraRecursive(childTransform.gameObject);
                if (foundInChild != null) return foundInChild;
            }
            return null;
        }
    }
}
