using System;
using System.Collections.Generic;
using System.Text;
using SkiaNodes.Interactive;

namespace SkiaNodes
{
    public class SKApp
    {
        public static SceneManager SceneManager => SceneManager.Current;

        public static SKInput InputManager => SKInput.Current;
    }
}
