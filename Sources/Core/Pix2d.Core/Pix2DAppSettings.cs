using System;
using System.Collections.Generic;

namespace Pix2d
{
    public class Pix2DAppSettings
    {
        public Pix2DAppMode AppMode { get; set; }

        public List<Type> Plugins { get; set; } = new List<Type>();
    }
}