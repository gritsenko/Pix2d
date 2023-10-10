using SkiaNodes.Interactive;

namespace SkiaNodes
{
    public class SKApp
    {
        public static SceneManager SceneManager => SceneManager.Current;

        public static SKInput InputManager => SKInput.Current;
    }
}
