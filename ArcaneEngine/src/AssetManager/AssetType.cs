namespace Arcane.AssetManager
{
    /// <summary>
    /// Defines the different types of assets the engine can manage.
    /// </summary>
    public enum AssetType
    {
        Unknown,    // Default or unrecognized
        Texture,    // .png, .jpg, .jpeg, .tga, .bmp
        Mesh,       // .obj, .fbx, .gltf (though importers are complex)
        Shader,     // .glsl, .vert, .frag, .shader
        Material,   // .mat (custom format)
        Scene,      // .arcscene (custom format)
        Sound,      // .wav, .ogg, .mp3
        Font,       // .ttf, .otf
        Text,       // .txt, .json, .xml, .csv
        Binary,     // Generic binary data, .bin
        Prefab,      // .prefab (custom format for GameObjects)
        HdriTexture
    }
}
