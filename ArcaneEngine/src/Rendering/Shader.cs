// Arcane/Rendering/Shader.cs
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Arcane.Core; // For Debug

namespace Arcane.Rendering
{
    public class Shader : IDisposable
    {
        public int ProgramId { get; private set; }
        private bool _isLoaded = false;
        private Dictionary<string, int> _uniformLocations = new Dictionary<string, int>();

        public Shader(string vertexPath, string fragmentPath, bool isFilePath = true)
        {
            string vertexShaderSource;
            string fragmentShaderSource;

            if (isFilePath)
            {
                // TODO: Implement file reading logic
                // vertexShaderSource = System.IO.File.ReadAllText(vertexPath);
                // fragmentShaderSource = System.IO.File.ReadAllText(fragmentPath);
                Debug.LogWarning($"Shader file loading not yet implemented. Paths: {vertexPath}, {fragmentPath}");
                // For now, use hardcoded basic shaders if paths are given
                vertexShaderSource = @"
                    #version 330 core
                    layout (location = 0) in vec3 aPosition;
                    // layout (location = 1) in vec3 aColor; // Example if color is per vertex
                    // out vec3 vertexColor;
                    uniform mat4 model;
                    uniform mat4 view;
                    uniform mat4 projection;
                    void main() { gl_Position = projection * view * model * vec4(aPosition, 1.0); /*vertexColor = aColor;*/ }";
                fragmentShaderSource = @"
                    #version 330 core
                    // in vec3 vertexColor;
                    out vec4 FragColor;
                    uniform vec3 objectColor; // Example uniform color
                    void main() { FragColor = vec4(objectColor, 1.0); /*FragColor = vec4(vertexColor, 1.0);*/ }";
            }
            else
            {
                vertexShaderSource = vertexPath; // Assume direct source code
                fragmentShaderSource = fragmentPath;
            }

            int vs = Compile(ShaderType.VertexShader, vertexShaderSource);
            int fs = Compile(ShaderType.FragmentShader, fragmentShaderSource);

            if (vs == 0 || fs == 0)
            {
                Debug.LogError("Shader creation failed due to compilation error(s).");
                GL.DeleteShader(vs); // Clean up if one compiled and other failed
                GL.DeleteShader(fs);
                return;
            }

            ProgramId = GL.CreateProgram();
            GL.AttachShader(ProgramId, vs);
            GL.AttachShader(ProgramId, fs);
            GL.LinkProgram(ProgramId);

            GL.GetProgram(ProgramId, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
            {
                Debug.LogError($"Shader Program Link Error: {GL.GetProgramInfoLog(ProgramId)}");
                GL.DeleteProgram(ProgramId);
                ProgramId = 0;
            }

            GL.DetachShader(ProgramId, vs);
            GL.DetachShader(ProgramId, fs);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            if (ProgramId != 0) _isLoaded = true;
        }

        private int Compile(ShaderType type, string source)
        {
            int shaderId = GL.CreateShader(type);
            GL.ShaderSource(shaderId, source);
            GL.CompileShader(shaderId);
            GL.GetShader(shaderId, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                Debug.LogError($"Shader Compilation Error ({type}): {GL.GetShaderInfoLog(shaderId)}");
                GL.DeleteShader(shaderId); // Important to delete failed shaders
                return 0;
            }
            return shaderId;
        }

        public void Use()
        {
            if (!_isLoaded) return;
            GL.UseProgram(ProgramId);
        }

        public void SetMatrix4(string name, Matrix4 matrix, bool transpose = true) // OpenTK matrices are row-major, GL expects column-major
        {
            if (!_isLoaded) return;
            GL.UniformMatrix4(GetUniformLocation(name), transpose, ref matrix);
        }

        public void SetVector3(string name, Vector3 value)
        {
            if (!_isLoaded) return;
            GL.Uniform3(GetUniformLocation(name), value);
        }

        public void SetFloat(string name, float value)
        {
            if (!_isLoaded) return;
            GL.Uniform1(GetUniformLocation(name), value);
        }


        private int GetUniformLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out int location))
            {
                return location;
            }
            location = GL.GetUniformLocation(ProgramId, name);
            if (location == -1)
            {
                // Debug.LogWarning($"Uniform '{name}' not found in shader program {ProgramId}.");
            }
            _uniformLocations[name] = location;
            return location;
        }


        public void Dispose()
        {
            if (!_isLoaded) return;
            GL.DeleteProgram(ProgramId);
            _isLoaded = false;
            // Debug.Log($"Shader disposed: ID={ProgramId}");
            GC.SuppressFinalize(this);
        }
        ~Shader()
        {
            // Debug.LogWarning($"Shader (ID {ProgramId}) not disposed by user.");
        }
    }
}
