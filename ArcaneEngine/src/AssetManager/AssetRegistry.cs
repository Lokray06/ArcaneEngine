// ArcaneEngine/src/AssetManager/AssetRegistry.cs
using Arcane.Core;
using System; // Required for Guid, StringComparer
using System.IO; // Required for Path, DirectoryInfo, FileInfo
using System.Collections.Generic; // Required for Dictionary, List

namespace Arcane.AssetManager
{
    public static class AssetRegistry
    {
        private static readonly Dictionary<Guid, Asset> _assetsById = new Dictionary<Guid, Asset>();
        private static readonly Dictionary<string, Asset> _assetsByNormalizedPath = new Dictionary<string, Asset>();
        // Using a dictionary of lists for names, as names might not be unique across different paths.
        private static readonly Dictionary<string, List<Asset>> _assetsByName = new Dictionary<string, List<Asset>>(StringComparer.OrdinalIgnoreCase);

        private static bool _isScanned = false;

        /// <summary>
        /// Scans the specified root directory and its subdirectories for asset files.
        /// Creates metadata for each found asset but does not load the actual data.
        /// This should typically be called once during engine initialization.
        /// </summary>
        /// <param name="rootDirectory">The root directory to scan for assets.</param>
        public static void ScanAssets(string rootDirectory)
        {
            if (_isScanned)
            {
                Debug.LogWarning("AssetRegistry.ScanAssets: Assets have already been scanned. Call ClearRegistry() first if a rescan is needed.");
                return;
            }
            if (!Directory.Exists(rootDirectory))
            {
                Debug.LogError($"AssetRegistry.ScanAssets: Root directory '{rootDirectory}' not found.");
                return;
            }

            Debug.Log($"AssetRegistry: Starting asset scan in '{rootDirectory}'...");
            _assetsById.Clear();
            _assetsByNormalizedPath.Clear();
            _assetsByName.Clear();

            ScanDirectoryRecursive(new DirectoryInfo(rootDirectory));

            _isScanned = true;
            Debug.Log($"AssetRegistry: Scan complete. Found {_assetsById.Count} potential assets.");
        }

        private static void ScanDirectoryRecursive(DirectoryInfo dirInfo)
        {
            foreach (FileInfo fileInfo in dirInfo.GetFiles())
            {
                AssetType type = DetermineAssetType(fileInfo.Extension);
                if (type != AssetType.Unknown)
                {
                    string assetName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    string normalizedPath = Path.GetFullPath(fileInfo.FullName); // Normalize for consistent key

                    if (_assetsByNormalizedPath.ContainsKey(normalizedPath))
                    {
                        Debug.LogWarning($"AssetRegistry: Asset with path '{normalizedPath}' already registered. Skipping duplicate.");
                        continue;
                    }

                    Asset asset = new Asset(normalizedPath, type, assetName);

                    _assetsById[asset.Id] = asset;
                    _assetsByNormalizedPath[normalizedPath] = asset;

                    if (!_assetsByName.TryGetValue(assetName, out List<Asset> nameList))
                    {
                        nameList = new List<Asset>();
                        _assetsByName[assetName] = nameList;
                    }
                    nameList.Add(asset);
                    // Debug.Log($"Registered Asset: '{asset.Name}' (Type: {asset.Type}, Path: {asset.FilePath})");
                }
            }

            foreach (DirectoryInfo subDirInfo in dirInfo.GetDirectories())
            {
                ScanDirectoryRecursive(subDirInfo);
            }
        }

        /// <summary>
        /// Determines the AssetType based on the file extension.
        /// </summary>
        private static AssetType DetermineAssetType(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return AssetType.Unknown;

            switch (extension.ToLowerInvariant()) // Use ToLowerInvariant for case-insensitive comparison
            {
                // Textures
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".bmp":
                    return AssetType.Texture;
                // HDR Textures
                case ".hdr":
                    return AssetType.HdriTexture;
                // Meshes
                case ".obj":
                    // case ".fbx": // Requires dedicated importer
                    // case ".gltf": // Requires dedicated importer
                    // case ".glb":  // Requires dedicated importer
                    return AssetType.Mesh;
                // Shaders
                case ".glsl":
                case ".shader":
                case ".vert":
                case ".frag":
                    return AssetType.Shader;
                // Materials (custom format)
                case ".mat":
                    return AssetType.Material;
                // Scenes (custom format)
                case ".arcscene":
                    return AssetType.Scene;
                // Sounds
                case ".wav":
                case ".ogg":
                case ".mp3":
                    return AssetType.Sound;
                // Fonts
                case ".ttf":
                case ".otf":
                    return AssetType.Font;
                // Text files
                case ".txt":
                case ".json":
                case ".xml":
                case ".csv":
                case ".ini":
                case ".md":
                    return AssetType.Text;
                // Binary files (generic)
                case ".bin":
                    return AssetType.Binary;
                // Prefabs (custom format)
                case ".prefab":
                    return AssetType.Prefab;
                default:
                    return AssetType.Unknown;
            }
        }

        /// <summary>
        /// Retrieves an asset's loaded data by its unique ID. Loads the asset if it's not already loaded.
        /// </summary>
        /// <typeparam name="T">The expected type of the asset's Data property.</typeparam>
        /// <param name="id">The GUID of the asset.</param>
        /// <returns>The loaded asset data as type T, or null if not found or type mismatch.</returns>
        public static T GetAsset<T>(Guid id) where T : class
        {
            if (_assetsById.TryGetValue(id, out Asset asset))
            {
                if (!asset.IsLoaded)
                {
                    asset.Load();
                }
                return asset.Data as T; // Returns null if Data is null or not convertible to T
            }
            Debug.LogWarning($"AssetRegistry.GetAsset: Asset with ID '{id}' not found.");
            return null;
        }

        /// <summary>
        /// Retrieves an asset's loaded data by its full normalized file path. Loads the asset if it's not already loaded.
        /// </summary>
        /// <typeparam name="T">The expected type of the asset's Data property.</typeparam>
        /// <param name="filePath">The full, normalized path to the asset file.</param>
        /// <returns>The loaded asset data as type T, or null if not found or type mismatch.</returns>
        public static T GetAssetByPath<T>(string filePath) where T : class
        {
            string normalizedPath = Path.GetFullPath(filePath); // Ensure path is normalized
            if (_assetsByNormalizedPath.TryGetValue(normalizedPath, out Asset asset))
            {
                if (!asset.IsLoaded)
                {
                    asset.Load();
                }
                return asset.Data as T;
            }
            Debug.LogWarning($"AssetRegistry.GetAssetByPath: Asset with path '{normalizedPath}' not found.");
            return null;
        }

        /// <summary>
        /// Retrieves the first asset's loaded data found with the given name. Loads the asset if it's not already loaded.
        /// Note: Asset names might not be unique. For guaranteed uniqueness, use GetAsset(Guid) or GetAssetByPath.
        /// </summary>
        /// <typeparam name="T">The expected type of the asset's Data property.</typeparam>
        /// <param name="name">The name of the asset (case-insensitive).</param>
        /// <returns>The loaded asset data as type T, or null if not found or type mismatch.</returns>
        public static T GetAssetByName<T>(string name) where T : class
        {
            if (_assetsByName.TryGetValue(name, out List<Asset> assetsWithName) && assetsWithName.Count > 0)
            {
                Asset asset = assetsWithName[0]; // Get the first one found
                if (assetsWithName.Count > 1)
                {
                    Debug.LogWarning($"AssetRegistry.GetAssetByName: Multiple assets found with name '{name}'. Returning the first one: '{asset.FilePath}'. Use ID or full path for unique retrieval.");
                }
                if (!asset.IsLoaded)
                {
                    asset.Load();
                }
                return asset.Data as T;
            }
            Debug.LogWarning($"AssetRegistry.GetAssetByName: Asset with name '{name}' not found.");
            return null;
        }

        /// <summary>
        /// Gets the metadata for an asset by its full normalized file path, without loading its data.
        /// </summary>
        /// <param name="filePath">The full, normalized path to the asset file.</param>
        /// <returns>The Asset object (metadata), or null if not found.</returns>
        public static Asset GetAssetMetadataByPath(string filePath)
        {
            string normalizedPath = Path.GetFullPath(filePath);
            _assetsByNormalizedPath.TryGetValue(normalizedPath, out Asset asset);
            if (asset == null) Debug.LogWarning($"AssetRegistry.GetAssetMetadataByPath: Asset metadata with path '{normalizedPath}' not found.");
            return asset;
        }

        /// <summary>
        /// Gets the metadata for the first asset found with the given name, without loading its data.
        /// Note: Asset names might not be unique. For guaranteed uniqueness, use GetAssetMetadataByPath or ID.
        /// </summary>
        /// <param name="name">The name of the asset (case-insensitive).</param>
        /// <returns>The Asset object (metadata), or null if not found.</returns>
        public static Asset GetAssetMetadataByName(string name)
        {
            if (_assetsByName.TryGetValue(name, out List<Asset> assetsWithName) && assetsWithName.Count > 0)
            {
                if (assetsWithName.Count > 1)
                {
                    Debug.LogWarning($"AssetRegistry.GetAssetMetadataByName: Multiple assets found with name '{name}'. Returning the first one: '{assetsWithName[0].FilePath}'. Use ID or full path for unique retrieval.");
                }
                return assetsWithName[0];
            }
            Debug.LogWarning($"AssetRegistry.GetAssetMetadataByName: Asset metadata with name '{name}' not found.");
            return null;
        }

        /// <summary>
        /// Gets the metadata for an asset by its ID, without loading its data.
        /// </summary>
        /// <param name="id">The GUID of the asset.</param>
        /// <returns>The Asset object (metadata), or null if not found.</returns>
        public static Asset GetAssetMetadataById(Guid id)
        {
            _assetsById.TryGetValue(id, out Asset asset);
            if (asset == null) Debug.LogWarning($"AssetRegistry.GetAssetMetadataById: Asset metadata with ID '{id}' not found.");
            return asset;
        }

        /// <summary>
        /// Unloads a specific asset by its ID, freeing its data from memory.
        /// </summary>
        /// <param name="id">The GUID of the asset to unload.</param>
        public static void UnloadAsset(Guid id)
        {
            if (_assetsById.TryGetValue(id, out Asset asset))
            {
                asset.Unload();
            }
        }

        /// <summary>
        /// Unloads all assets that are currently loaded, freeing their data from memory.
        /// </summary>
        public static void UnloadAllLoadedAssets()
        {
            Debug.Log("AssetRegistry: Unloading all loaded assets...");
            foreach (Asset asset in _assetsById.Values)
            {
                if (asset.IsLoaded)
                {
                    asset.Unload();
                }
            }
            Debug.Log("AssetRegistry: Unload all complete.");
        }

        /// <summary>
        /// Clears all registered asset metadata. Does not unload already loaded assets unless explicitly called.
        /// Call UnloadAllLoadedAssets() first if memory needs to be freed.
        /// </summary>
        public static void ClearRegistry()
        {
            _assetsById.Clear();
            _assetsByNormalizedPath.Clear();
            _assetsByName.Clear();
            _isScanned = false;
            Debug.Log("AssetRegistry: Registry cleared.");
        }
    }
}
