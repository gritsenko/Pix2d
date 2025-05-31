using System.Text;

namespace Pix2d.Plugins.Psd.PsdReader
{
    public class BinaryReverseReader : BinaryReader
    {
        public BinaryReverseReader(Stream stream) : base(stream)
        {
        }

        public override short ReadInt16() => Utilities.SwapBytes(base.ReadInt16());

        public override int ReadInt32() => Utilities.SwapBytes(base.ReadInt32());

        public override long ReadInt64() => Utilities.SwapBytes(base.ReadInt64());

        public override ushort ReadUInt16() => Utilities.SwapBytes(base.ReadUInt16());

        public override uint ReadUInt32() => Utilities.SwapBytes(base.ReadUInt32());

        public override ulong ReadUInt64() => Utilities.SwapBytes(base.ReadUInt64());

        public string ReadPascalString()
        {
            var b = base.ReadByte();
            var bytes = base.ReadBytes(b);
            if (b % 2 == 0)
            {
                base.ReadByte();
            }
            return Encoding.Default.GetString(bytes);
        }
    }
}