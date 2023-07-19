namespace SkiaNodes.Interactive;

public static class VirtualKeysExtension
{
    public static KeyModifier ToModifier(this VirtualKeys key)
    {
        switch (key)
        {
            case VirtualKeys.OEMWSCtrl:
            case VirtualKeys.Control:
            case VirtualKeys.LeftControl:
            case VirtualKeys.RightControl:
                return KeyModifier.Ctrl;
            case VirtualKeys.Shift:
            case VirtualKeys.LeftShift:
            case VirtualKeys.RightShift:
                return KeyModifier.Shift;
            case VirtualKeys.LeftWindows:
            case VirtualKeys.RightWindows:
                return KeyModifier.Win;
            case VirtualKeys.Menu:
            case VirtualKeys.LeftMenu:
            case VirtualKeys.RightMenu:
                return KeyModifier.Alt;
        }

        return KeyModifier.None;
    }
}