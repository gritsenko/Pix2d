using System;
using System.Text;
using SkiaNodes.Interactive;

namespace Pix2d.Primitives
{
    public class CommandShortcut
    {
        public KeyModifier KeyModifiers { get; private set; }

        public VirtualKeys Key { get; private set; }
        public Func<VirtualKeys, string> KeyConverter { get; set; }

        public CommandShortcut(VirtualKeys key, KeyModifier keyModifiers = KeyModifier.None)
        {
            KeyModifiers = keyModifiers;
            Key = key;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if ((KeyModifiers & KeyModifier.Ctrl) != 0)
            {
                sb.Append("Ctrl");
            }

            if ((KeyModifiers & KeyModifier.Alt) != 0)
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append("Alt");
            }

            if ((KeyModifiers & KeyModifier.Shift) != 0)
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append("Shift");
            }

            if ((KeyModifiers & KeyModifier.Win) != 0)
            {
                if (sb.Length > 0)
                    sb.Append("+");
                sb.Append("Win");
            }

            if (sb.Length > 0)
                sb.Append("+");

            var key = Key.ToString();

            if (KeyConverter != null)
            {
                key = KeyConverter(Key)?.ToUpper();
            }

            sb.Append(!string.IsNullOrWhiteSpace(key) ? key : Key.ToString());

            return sb.ToString();
        }
    }
}