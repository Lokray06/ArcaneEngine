// ArcaneEngine/src/Rendering/Shader.cs
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.IO; // For File operations
using System.Collections.Generic; // For Dictionary
using Arcane.Core; // For Debug
// Assuming GLDebug is in Arcane.Rendering or Arcane.Core
// If GLDebug is in Arcane.Renderering (with three 'r's), adjust this using.
// For consistency, let's assume Arcane.Rendering.GLDebug
using Arcane.Rendering;


namespace Arcane.Rendering
{
    public class Shader : IDisposable
    {
        public int ProgramId { get; private set; }
        private bool _isDisposed = false; // Renamed from isDisposed

        private readonly Dictionary<string, int> _uniformLocations = new Dictionary<string, int>(); // Renamed

        // Default hardcoded shaders (your existing simple ones)
        // These might be used if loading the PBR shaders fails or for other simple materials.
        private const string FallbackVertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 a_Position; // Changed from aPosition for consistency
            uniform mat4 u_ModelMatrix;  // Changed from model
            uniform mat4 u_ViewMatrix;   // Changed from view
            uniform mat4 u_ProjectionMatrix; // Changed from projection
            void main()
            {
                gl_Position = u_ProjectionMatrix * u_ViewMatrix * u_ModelMatrix * vec4(a_Position, 1.0);
            }";

        private const string FallbackFragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;
            uniform vec3 u_ObjectColor; // Changed from objectColor
            void main()
            {
                FragColor = vec4(u_ObjectColor, 1.0);
            }";

        public Shader(string vertexSource, string fragmentSource)
        {
            LoadFromSource(vertexSource, fragmentSource);
        }

        public Shader(string vertexPath, string fragmentPath, bool isFilePath)
        {
            if (isFilePath)
            {
                string vertSource = null;
                string fragSource = null;
                bool loadedSuccessfully = false;

                try
                {
                    if (File.Exists(vertexPath))
                    {
                        vertSource = File.ReadAllText(vertexPath);
                    }
                    else
                    {
                        Debug.LogError($"Shader: Vertex shader file not found at '{vertexPath}'.");
                    }

                    if (File.Exists(fragmentPath))
                    {
                        fragSource = File.ReadAllText(fragmentPath);
                    }
                    else
                    {
                        Debug.LogError($"Shader: Fragment shader file not found at '{fragmentPath}'.");
                    }

                    if (vertSource != null && fragSource != null)
                    {
                        LoadFromSource(vertSource, fragSource);
                        if (ProgramId != 0) // Check if compilation and linking were successful
                        {
                            Debug.Log($"Shader: Successfully loaded and compiled from files: '{vertexPath}', '{fragmentPath}'. Program ID: {ProgramId}");
                            loadedSuccessfully = true;
                        }
                        else
                        {
                            Debug.LogError($"Shader: Failed to compile/link shaders from files: '{vertexPath}', '{fragmentPath}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Shader: Exception loading from file. Paths: '{vertexPath}', '{fragmentPath}'. Error: {ex.Message}");
                }
                finally // Fallback logic
                {
                    if (!loadedSuccessfully)
                    {
                        Debug.LogWarning($"Shader: Falling back to default internal shaders.");
                        LoadFromSource(FallbackVertexShaderSource, FallbackFragmentShaderSource);
                        if (ProgramId == 0)
                        {
                            Debug.LogError($"Shader: CRITICAL - Failed to compile/link even the fallback default shaders.");
                        }
                    }
                }
            }
            else // Parameters are direct source code
            {
                LoadFromSource(vertexPath ?? FallbackVertexShaderSource,
                               fragmentPath ?? FallbackFragmentShaderSource);
                if (ProgramId == 0)
                {
                    Debug.LogError($"Shader: CRITICAL - Failed to compile/link shaders from provided source strings.");
                }
            }
        }

        private void LoadFromSource(string vertexSource, string fragmentSource)
        {
            int vertexShader = CompileShader(vertexSource, ShaderType.VertexShader);
            int fragmentShader = CompileShader(fragmentSource, ShaderType.FragmentShader);

            if (vertexShader == 0 || fragmentShader == 0)
            {
                ProgramId = 0;
                // CompileShader already logs errors and deletes failed shader objects.
                return;
            }

            ProgramId = LinkProgram(vertexShader, fragmentShader);

            // Individual shader objects are no longer needed after linking.
            if (ProgramId != 0) // Only detach if linking was successful
            {
                GL.DetachShader(ProgramId, vertexShader);
                GL.DetachShader(ProgramId, fragmentShader);
            }
            // Always delete the shader objects as they are now part of the program or failed.
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            GLDebug.CheckError("Shader.LoadFromSource - After deleting individual shaders");
        }

        private int CompileShader(string source, ShaderType type)
        {
            int shaderId = GL.CreateShader(type);
            GL.ShaderSource(shaderId, source);
            GL.CompileShader(shaderId);
            GLDebug.CheckError($"Shader.CompileShader - After GL.CompileShader for {type}");

            GL.GetShader(shaderId, ShaderParameter.CompileStatus, out int compileStatus);
            if (compileStatus == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shaderId);
                Debug.LogError($"Shader Compilation Error ({type}):\n{infoLog}\nSource (first 500 chars):\n{source.Substring(0, Math.Min(source.Length, 500))}...");
                GL.DeleteShader(shaderId);
                return 0;
            }
            return shaderId;
        }

        private int LinkProgram(int vertexShader, int fragmentShader)
        {
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);
            GLDebug.CheckError("Shader.LinkProgram - After GL.LinkProgram");

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                Debug.LogError($"Shader Program Linking Error:\n{infoLog}");
                GL.DeleteProgram(program);
                return 0;
            }
            return program;
        }

        public void Use()
        {
            if (_isDisposed || ProgramId == 0) return;
            GL.UseProgram(ProgramId);
            GLDebug.CheckError("Shader.Use - After GL.UseProgram");
        }

        private int GetUniformLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out int location))
            {
                return location;
            }
            if (ProgramId == 0) return -1; // No warning here, Apply() will warn if shader is invalid

            location = GL.GetUniformLocation(ProgramId, name);
            // GLDebug.CheckError($"Shader.GetUniformLocation - For uniform '{name}' in ProgramID {ProgramId}"); // Can be spammy
            if (location == -1)
            {
                // Debug.LogWarning($"Shader.GetUniformLocation: Uniform '{name}' not found or not active in shader program {ProgramId}. This is normal for unused uniforms.");
            }
            _uniformLocations[name] = location;
            return location;
        }

        public void SetInt(string name, int value)
        {
            if (_isDisposed || ProgramId == 0) return;
            int location = GetUniformLocation(name);
            if (location != -1) GL.Uniform1(location, value);
        }

        public void SetFloat(string name, float value)
        {
            if (_isDisposed || ProgramId == 0) return;
            int location = GetUniformLocation(name);
            if (location != -1) GL.Uniform1(location, value);
        }

        public void SetVector2(string name, Vector2 value)
        {
            if (_isDisposed || ProgramId == 0) return;
            int location = GetUniformLocation(name);
            if (location != -1) GL.Uniform2(location, value);
        }
        public void SetVector3(string name, Vector3 value)
        {
            if (_isDisposed || ProgramId == 0) return;
            int location = GetUniformLocation(name);
            if (location != -1) GL.Uniform3(location, value);
        }

        public void SetVector4(string name, Vector4 value)
        {
            if (_isDisposed || ProgramId == 0) return;
            int location = GetUniformLocation(name);
            if (location != -1) GL.Uniform4(location, value);
        }

        public void SetMatrix4(string name, Matrix4 matrix, bool transpose = false)
        {
            if (_isDisposed || ProgramId == 0) return;
            int location = GetUniformLocation(name);
            if (location != -1)
            {
                GL.UniformMatrix4(location, transpose, ref matrix);
            }
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
                if (disposing) { /* No managed resources to dispose directly */ }
                if (ProgramId != 0)
                {
                    GL.DeleteProgram(ProgramId);
                    GLDebug.CheckError("Shader.Dispose - After GL.DeleteProgram");
                    ProgramId = 0;
                }
                _isDisposed = true;
            }
        }

        ~Shader()
        {
            Dispose(disposing: false);
        }
    }
}
