namespace SkiaNodes.Interactive;

public class PointerActionEventArgs
{
    public PointerActionType ActionType { get; }
    public SKInputPointer Pointer { get; }

    public KeyModifier KeyModifiers { get; }

    public bool Handled { get; set; }

    public PointerActionEventArgs(PointerActionType actionType, SKInputPointer pointer, KeyModifier modifier)
    {
            KeyModifiers = modifier;
            ActionType = actionType;
            Pointer = pointer;
        }
}