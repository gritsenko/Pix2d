using System;

namespace Pix2d.Abstract.Tools;

public class Pix2dToolAttribute : Attribute
{
    public bool HasSettings { get; set; }
    public bool EnabledDuringAnimation { get; set; }
}