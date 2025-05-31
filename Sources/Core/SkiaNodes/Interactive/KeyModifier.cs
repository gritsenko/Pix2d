using System;

namespace SkiaNodes.Interactive;

[Flags]
public enum KeyModifier
{
    None = 0b0000,
    Alt = 0b0001,
    Ctrl = 0b0010,
    Shift = 0b0100,
    Win = 0b1000
}