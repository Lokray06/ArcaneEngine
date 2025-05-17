// In Debug.cs or a new static GLDebug.cs
using OpenTK.Graphics.OpenGL4;
using System; // For Console.WriteLine if Debug.LogError isn't static or available easily

namespace Arcane.Rendering
{
    public static class GLDebug
    {
        public static void CheckError(string contextMessage = "")
        {
            ErrorCode errorCode = GL.GetError();
            if (errorCode != ErrorCode.NoError)
            {
                string errorStr = errorCode.ToString();
                // Use your existing Debug.LogError or Console.WriteLine
                Arcane.Core.Debug.LogError($"OpenGL Error | Context: {contextMessage} | Code: {errorCode} ({errorStr})");
                // Optionally, you could throw an exception here during development to halt immediately
                // throw new Exception($"OpenGL Error ({contextMessage}): {errorCode}");
            }
        }
    }
}