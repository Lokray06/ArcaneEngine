// ArcaneEngine/src/Rendering/Texture.cs
using OpenTK.Graphics.OpenGL4;
using System;
using Arcane.Core; // For Debug
// Make sure AssetManager types are accessible
using Arcane.AssetManager;

namespace Arcane.Rendering
{
    public class Texture : IDisposable
    {
        public int Id { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string FilePath { get; private set; }

        private bool _isDisposed = false;

        /// <summary>
        /// Default constructor for internal use or invalid texture state.
        /// </summary>
        public Texture()
        {
            this.Id = 0;
            this.Width = 0;
            this.Height = 0;
            this.FilePath = null;
            this._isDisposed = true; // Mark as not usable initially
        }

        /// <summary>
        /// Initializes a new instance of the Texture class with an existing OpenGL texture ID.
        /// Typically used for textures managed externally or default textures.
        /// </summary>
        public Texture(int id, int width, int height, string filePath = null)
        {
            if (id <= 0)
            {
                // Debug.LogWarning($"Texture: Initialized with potentially invalid ID: {id}");
            }
            Id = id;
            Width = width;
            Height = height;
            FilePath = filePath;
            _isDisposed = (id == 0); // If ID is 0, consider it already disposed/invalid
        }

        /// <summary>
        /// Initializes a new GPU Texture from an Asset object.
        /// Loads the asset data if not already loaded and uploads it to the GPU.
        /// </summary>
        /// <param name="textureAsset">The asset metadata object for the texture.</param>
        public Texture(Asset textureAsset) : this() // Chain to default constructor
        {
            if (textureAsset == null)
            {
                Debug.LogError("Rendering.Texture: Cannot create texture from a null Asset.");
                return; // Remains in invalid state from default constructor
            }

            if (textureAsset.Type != AssetType.Texture)
            {
                Debug.LogError($"Rendering.Texture: Asset '{textureAsset.Name}' is not of type Texture (Type: {textureAsset.Type}). Cannot create GPU texture.");
                return; // Remains in invalid state
            }

            if (!textureAsset.IsLoaded)
            {
                // Debug.Log($"Rendering.Texture: Asset '{textureAsset.Name}' data not loaded. Calling Load().");
                textureAsset.Load();
            }

            if (!textureAsset.IsLoaded || !(textureAsset.Data is TextureData texData))
            {
                Debug.LogError($"Rendering.Texture: Failed to load or cast asset data to Importer.TextureData for asset '{textureAsset.Name}'.");
                return; // Remains in invalid state
            }

            // Now, texData is Arcane.AssetManager.Importer.TextureData
            // Proceed with OpenGL texture creation
            this.FilePath = textureAsset.FilePath;
            this.Width = texData.Width;
            this.Height = texData.Height;

            Id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Id);
            GLDebug.CheckError($"Rendering.Texture Constructor (Asset) - GenTexture/BindTexture for '{textureAsset.Name}'");


            PixelInternalFormat internalFormat;
            PixelFormat pixelDataFormat; // Format of the data in texData.PixelData

            // Assuming Importer.LoadTexture with StbImageSharp (using ColorComponents.RedGreenBlueAlpha)
            // always provides 4 channels (RGBA) in texData.PixelData.
            if (texData.Channels == 4) // Data is RGBA
            {
                internalFormat = PixelInternalFormat.Rgba8; // Store as RGBA8 on GPU
                pixelDataFormat = PixelFormat.Rgba;         // Source data is RGBA
            }
            else if (texData.Channels == 3) // Data is RGB
            {
                internalFormat = PixelInternalFormat.Rgb8;  // Store as RGB8 on GPU
                pixelDataFormat = PixelFormat.Rgb;          // Source data is RGB
            }
            // StbImageSharp with ColorComponents.RedGreenBlueAlpha should ensure 4 channels.
            // If you change Importer to load other formats (e.g. grayscale), add cases here.
            else
            {
                Debug.LogWarning($"Rendering.Texture: Unexpected number of channels ({texData.Channels}) from Importer for '{textureAsset.Name}'. Assuming RGBA source data if PixelData is not null.");
                internalFormat = PixelInternalFormat.Rgba8;
                pixelDataFormat = PixelFormat.Rgba; // Default assumption
                if (texData.PixelData == null || (texData.Width * texData.Height * 4) != texData.PixelData.Length && texData.Channels != 4)
                {
                    Debug.LogError($"Rendering.Texture: Pixel data for '{textureAsset.Name}' has unexpected size or channel count does not match RGBA assumption. Texture may be incorrect.");
                    GL.DeleteTexture(Id); Id = 0; return; // Critical error
                }
            }

            // Upload the texture data
            GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, Width, Height, 0, pixelDataFormat, PixelType.UnsignedByte, texData.PixelData);
            GLDebug.CheckError($"Rendering.Texture Constructor (Asset) - After TexImage2D for '{textureAsset.Name}'");

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GLDebug.CheckError($"Rendering.Texture Constructor (Asset) - After GenerateMipmap for '{textureAsset.Name}'");

            GL.BindTexture(TextureTarget.Texture2D, 0);
            _isDisposed = false; // Successfully created
            // Debug.Log($"Rendering.Texture: Successfully created GPU texture ID {Id} for asset '{textureAsset.Name}'.");
        }

        /// <summary>
        /// Creates a 2D HDR Texture from HdrTextureData.
        /// </summary>
        public Texture(HdrTextureData hdrData, string name = "HDRTexture")
        {
            if (hdrData == null || hdrData.PixelData == null)
            {
                Debug.LogError($"Texture(HdrTextureData): HDR data is null for '{name}'.");
                return; // Remains invalid
            }

            this.Id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, this.Id);

            // For HDR, use floating point internal format and pixel type
            PixelInternalFormat internalFormat = PixelInternalFormat.Rgb16f; // Common for HDR
            PixelFormat pixelDataFormat = PixelFormat.Rgb; // Assuming 3 channels (RGB) from HdrTextureData
            if (hdrData.Channels != 3)
            {
                Debug.LogWarning($"Texture(HdrTextureData): HDR data for '{name}' has {hdrData.Channels} channels, expected 3. Adjusting format if possible or may error.");
                // Add more sophisticated channel handling if necessary
            }

            GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, hdrData.Width, hdrData.Height, 0,
                          pixelDataFormat, PixelType.Float, hdrData.PixelData);
            GLDebug.CheckError($"Texture(HdrTextureData) - TexImage2D for '{name}'");

            this.Width = hdrData.Width;
            this.Height = hdrData.Height;
            this.FilePath = name; // Or pass original asset path if available

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.BindTexture(TextureTarget.Texture2D, 0);
            this._isDisposed = (this.Id == 0);
            if (!this._isDisposed) Debug.Log($"HDR Texture '{name}' created. ID: {this.Id}");
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            if (_isDisposed || Id == 0) return;
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Id);
        }

        public void Unbind(TextureUnit unit = TextureUnit.Texture0)
        {
            if (_isDisposed) return;
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, 0);
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
                if (disposing)
                {
                    // No managed resources to dispose directly in this simple class
                }

                // Actual deletion should be handled by an AssetManager or a TextureCache
                // if the texture is truly no longer needed by any material.
                // However, if this Texture object was created from an Asset, it "owns" this specific GPU instance.
                // Default textures created in Material.cs have their own lifecycle.
                if (Id != 0)
                {
                    // Check if it's not a known default static texture before deleting.
                    // This check is tricky without more context. For now, assume if created via Asset, it's owned.
                    // Default textures in Material.cs manage their own static _defaultWhiteTexture.Id etc.
                    // This Texture instance's Id should be deleted if it's not one of those.
                    // A simple flag or checking against known default IDs might be needed if they could be passed to this Dispose.
                    // However, this Dispose is for instances of *this* class.
                    GL.DeleteTexture(Id);
                    GLDebug.CheckError($"Texture.Dispose - DeleteTexture ID: {Id}");
                    // Debug.Log($"Rendering.Texture: Disposed GPU texture ID {Id}, Path: {FilePath}");
                }

                Id = 0; // Mark as invalid/disposed
                _isDisposed = true;
            }
        }

        ~Texture()
        {
            Dispose(false);
        }
    }
}