using System;

namespace SkiaNodes.Interactive;

public class KeyboardActionEventArgs : EventArgs
{
    public VirtualKeys Key;
    public KeyModifier Modifiers;
    public bool Handled;

    public KeyboardActionEventArgs(VirtualKeys key, KeyModifier modifiers)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public bool IsPressedOnly(KeyModifier modifiers)
    {
        return modifiers == Modifiers;
    }
    public bool IsPressed(KeyModifier modifiers)
    {
        return (modifiers & Modifiers) > 0;
    }
}