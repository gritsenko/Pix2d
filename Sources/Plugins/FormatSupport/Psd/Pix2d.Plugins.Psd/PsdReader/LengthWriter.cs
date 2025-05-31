namespace Pix2d.Plugins.Psd.PsdReader
{
    internal class LengthWriter : IDisposable
    {
        public LengthWriter(BinaryReverseWriter writer)
        {
            _reverseWriter = writer;
            _lengthPosition = _reverseWriter.BaseStream.Position;
            _reverseWriter.Write(4277010157u);
            _startPosition = _reverseWriter.BaseStream.Position;
        }

        public void Write()
        {
            if (_lengthPosition == -9223372036854775808L)
            {
                return;
            }
            var position = _reverseWriter.BaseStream.Position;
            _reverseWriter.BaseStream.Position = _lengthPosition;
            var num = position - _startPosition;
            _reverseWriter.Write((uint)num);
            _reverseWriter.BaseStream.Position = position;
            _lengthPosition = long.MinValue;
        }

        public void Dispose()
        {
            Write();
        }

        private long _lengthPosition = long.MinValue;

        private readonly long _startPosition;

        private readonly BinaryReverseWriter _reverseWriter;
    }
}