namespace Pix2d.Plugins.Psd.PsdReader
{
    public class BinaryReverseWriter : BinaryWriter
    {
        public bool AutoFlush { get; set; }

        public BinaryReverseWriter(Stream stream) : base(stream)
        {
        }

        public void WritePascalString(string s)
        {
            var array = (s.Length > 255) ? s.Substring(0, 255).ToCharArray() : s.ToCharArray();
            base.Write((byte)array.Length);
            base.Write(array);
            var num = array.Length + 1;
            if (num % 2 == 0)
            {
                return;
            }
            for (var i = 0; i < 2 - num % 2; i++)
            {
                base.Write(0);
            }
            if (AutoFlush)
            {
                Flush();
            }
        }

        public override void Write(short val)
        {
            val = Utilities.SwapBytes(val);
            base.Write(val);
            if (AutoFlush)
            {
                Flush();
            }
        }

        public override void Write(int val)
        {
            val = Utilities.SwapBytes(val);
            base.Write(val);
            if (AutoFlush)
            {
                Flush();
            }
        }

        public override void Write(long val)
        {
            val = Utilities.SwapBytes(val);
            base.Write(val);
            if (AutoFlush)
            {
                Flush();
            }
        }

        public override void Write(ushort val)
        {
            val = Utilities.SwapBytes(val);
            base.Write(val);
            if (AutoFlush)
            {
                Flush();
            }
        }

        public override void Write(uint val)
        {
            val = Utilities.SwapBytes(val);
            base.Write(val);
            if (AutoFlush)
            {
                Flush();
            }
        }

        public override void Write(ulong val)
        {
            val = Utilities.SwapBytes(val);
            base.Write(val);
            if (AutoFlush)
            {
                Flush();
            }
        }
    }
}