using Arcane.SceneSystem;

namespace Arcane.Renderering
{
    public interface Renderer
    {
        public void Init();
        public void Render(GameObject camera, Scene scene);
        public void Cleanup();
    }
}