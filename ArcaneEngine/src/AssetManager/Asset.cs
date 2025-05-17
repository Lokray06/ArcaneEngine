using Arcane.Core;
using System;

namespace Arcane.AssetManager
{
    public class Asset : IDisposable
    {
        public Guid Id { get; }
        public string Name { get; set; }
        public string FilePath { get; }
        public AssetType Type { get; }
        public object Data { get; private set; }
        public bool IsLoaded => Data != null;
        private bool _isDisposed = false;

        public Asset(string filePath, AssetType type, string name)
        {
            Id = Guid.NewGuid();
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Type = type;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Data = null;
        }

        internal void Load()
        {
            if (IsLoaded || _isDisposed) return;
            try
            {
                switch (Type)
                {
                    case AssetType.Texture:
                        Data = Importer.LoadTexture(FilePath);
                        break;
                    case AssetType.Mesh:
                        Data = Importer.LoadMeshFromOBJ(FilePath); // Correctly calls the updated OBJ loader
                        break;
                    case AssetType.Shader:
                        Data = Importer.LoadShaderFromFile(FilePath);
                        break;
                    case AssetType.HdriTexture:
                        Data = Importer.LoadHdrTextureData(FilePath);
                        break;
                    case AssetType.Text:
                        Data = Importer.LoadText(FilePath);
                        break;
                    default:
                        Debug.LogWarning($"Asset.Load: No importer for type '{Type}' ('{Name}').");
                        Data = null;
                        break;
                }
                if (Data == null && Type != AssetType.Unknown)
                {
                    Debug.LogError($"Asset.Load: Importer for '{Type}' returned null for '{Name}'. Load failed.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Asset.Load: Exception for '{Name}' ('{Type}'): {ex.Message}");
                Data = null;
            }
        }

        internal void Unload()
        {
            if (!IsLoaded || _isDisposed) return;
            if (Data is IDisposable disposableData)
            {
                try { disposableData.Dispose(); }
                catch (Exception ex) { Debug.LogError($"Asset.Unload: Error disposing data for '{Name}': {ex.Message}"); }
            }
            Data = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing) Unload();
                _isDisposed = true;
            }
        }
        ~Asset() { Dispose(false); }
    }
}