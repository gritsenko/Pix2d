using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SkiaSharp;

namespace SkiaNodes.Interactive
{
    public class SKInput
    {
        private static SKInput _instance;


        public Func<SKNode> RootNodeProvider { get; set; }
        public Func<ViewPort> ViewPortProvider { get; set; }


        private KeyModifier _keyModifiers;
        private bool _panMode;
        private SKInputPointer _pointer;

        public event EventHandler<KeyboardActionEventArgs> KeyPressed;
        public event EventHandler<KeyboardActionEventArgs> KeyReleased;
        public event EventHandler<RootNodeChangedEventArgs> RootNodeChanged;
        public event EventHandler<SKInputPointer> PointerChanged;

        public static SKInput Current => _instance ?? (_instance = new SKInput());

        public IInteractive CapturedPointerBy { get; set; }
        private IInteractive LastInteractiveUnderPointer { get; set; }

        public SKInputPointer Pointer
        {
            get => _pointer;
            private set
            {
                _pointer = value;
                PointerChanged?.Invoke(this, _pointer);
            }
        }

        public bool EnablePanWithSpace => true;

        public bool PanMode
        {
            get => _panMode;
            set
            {
                if (_panMode != value)
                {
                    _panMode = value;

                    OnPanModeChanged(value);
                }
            }
        }

        public bool IsInitialized => RootNodeProvider != null && ViewPortProvider != null;
        public bool EraserMode { get; set; }

        private void OnPanModeChanged(bool value)
        {
            foreach (var interactive in GetInteractives(Pointer.WorldPosition))
            {
                if (CapturedPointerBy == null || CapturedPointerBy == interactive)
                {
                    interactive.OnPanModeChanged(value);
                }
            }
        }

        public void SetPointerPressed(SKPoint pos, KeyModifier modifiers, bool isTouch, int clickCount = 1)
        {
            if (!IsInitialized)
                return;

            Pointer = new SKInputPointer(pos, GetViewport(), true, EraserMode, true);
            var args = new PointerActionEventArgs(PointerActionType.Pressed, Pointer, modifiers);

            HandlePointerEventByInteractives((interactive) =>
            {
                interactive?.OnPointerPressed(args, clickCount);
            }, args);
        }

        private ViewPort GetViewport() => ViewPortProvider?.Invoke();
        
        private void HandlePointerEventByInteractives(Action<IInteractive> handler, PointerActionEventArgs args)
        {
#if(DEBUG)
            if (args.ActionType == PointerActionType.Pressed)
            {
                var wtf = 0;
            }
#endif
            var interactives = GetInteractives(Pointer.WorldPosition);

            foreach (var interactive in interactives)
            {
                if (CapturedPointerBy == null || CapturedPointerBy == interactive)
                    handler.Invoke(interactive);

                if (args.Handled)
                {
//                    Debug.WriteLine("Handled by :" + interactive.GetType().Name);
                    break;
                }
            }
        }

        public void SetPointerReleased(SKPoint pos, KeyModifier modifiers, bool isTouch)
        {
            if (!IsInitialized)
                return;

            Pointer = new SKInputPointer(pos, GetViewport(), false, EraserMode, isTouch);
            var args = new PointerActionEventArgs(PointerActionType.Released, Pointer, modifiers);

            HandlePointerEventByInteractives((interactive) =>
            {
                interactive?.OnPointerReleasedCore(args);
            }, args);
        }

        public void SetPointerMoved(SKPoint pos, bool isPointerPressed, KeyModifier modifiers, bool isTouch)
        {
            if (!IsInitialized)
                return;

            Pointer = new SKInputPointer(pos, GetViewport(), isPointerPressed, EraserMode, isTouch);
            var worldPos = Pointer.WorldPosition;

            var args = new PointerActionEventArgs(PointerActionType.Moved, Pointer, modifiers);

            HandlePointerEventByInteractives((interactive) =>
            {
                interactive?.OnPointerMoved(args);
                if (interactive != LastInteractiveUnderPointer)
                {
                    LastInteractiveUnderPointer?.OnPointerLeave(worldPos);
                    LastInteractiveUnderPointer = interactive;
                    LastInteractiveUnderPointer?.OnPointerEnter(worldPos);
                    LastInteractiveUnderPointer = interactive;
                }

            }, args);
        }

        public IEnumerable<IInteractive> GetInteractives(SKPoint pos)
        {
            var rootNode = RootNodeProvider?.Invoke();
            if (rootNode == null)
                return Enumerable.Empty<IInteractive>();
            
            return rootNode
                .GetVisibleDescendants(x => (x.IsInteractive && x.ContainsPoint(pos)) || x == CapturedPointerBy, true, true)
                .Reverse();
        }

        public void CapturePointer(SKNode catchedBy)
        {
            CapturedPointerBy = catchedBy;
        }

        public void ReleasePointer(SKNode catchedBy)
        {
            if (CapturedPointerBy == catchedBy)
                CapturedPointerBy = null;
        }

        public bool SetKeyPressed(VirtualKeys key, KeyModifier keyModifiers)
        {
            var activeKeyModifier = key.ToModifier();
            if (activeKeyModifier == KeyModifier.None)
            {
                _keyModifiers = keyModifiers;
            }
            else
            {
                // The modifier key was released. CrossPlatformDesktop reports modifier to be still inactive but we need to set
                // is as currently active.
                _keyModifiers |= activeKeyModifier;
            }
            
            return OnKeyPressed(new KeyboardActionEventArgs(key, keyModifiers));
        }

        public bool SetKeyReleased(VirtualKeys key, KeyModifier keyModifiers)
        {
            var activeKeyModifier = key.ToModifier();
            if (activeKeyModifier == KeyModifier.None)
            {
                _keyModifiers = keyModifiers;
            }
            else
            {
                // The modifier key was released. CrossPlatformDesktop reports modifier to be still active but we need to set
                // is as currently inactive.
                _keyModifiers &= ~activeKeyModifier;
            }
            
            return OnKeyReleased(new KeyboardActionEventArgs(key, keyModifiers));
        }

        private bool OnKeyPressed(KeyboardActionEventArgs e)
        {
            if (KeyPressed == null)
                return false;

            var ds = KeyPressed.GetInvocationList();
            foreach (var d in ds.OfType<EventHandler<KeyboardActionEventArgs>>())
            {
                d.Invoke(this, e);
                if (e.Handled)
                {
                    Debug.WriteLine($"Key pressed {e.Key} processed by " + d.Target.GetType().Name);
                    break;
                }
            }
            return e.Handled;
        }

        protected virtual bool OnKeyReleased(KeyboardActionEventArgs e)
        {
            if (KeyReleased == null)
                return false;

            var ds = KeyReleased.GetInvocationList();
            foreach (var d in ds.OfType<EventHandler<KeyboardActionEventArgs>>())
            {
                d.Invoke(this, e);
                if (e.Handled)
                {
                    Debug.WriteLine($"Key released {e.Key} processed by " + d.Target.GetType().Name);
                    break;
                }
            }

            return e.Handled;
        }

        public KeyModifier GetModifiers()
        {
            return _keyModifiers;
        }

        protected virtual void OnRootNodeChanged(RootNodeChangedEventArgs e)
        {
            RootNodeChanged?.Invoke(this, e);
        }
    }
}
