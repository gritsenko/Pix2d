namespace Pix2d.Plugins.Psd.PsdReader
{
    internal class RleHelper
    {
        public static int EncodedRow(Stream stream, byte[] imgData, int startIdx, int columns)
        {
            var position = stream.Position;
            var rlePacketStateMachine = new RlePacketStateMachine(stream);
            for (var i = 0; i < columns; i++)
            {
                rlePacketStateMachine.Push(imgData[i + startIdx]);
            }
            rlePacketStateMachine.Flush();
            return (int)(stream.Position - position);
        }

        public static void DecodedRow(Stream stream, byte[] imgData, int startIdx, int columns)
        {
            var i = 0;
            while (i < columns)
            {
                var b = (byte)stream.ReadByte();
                var num = (int)b;
                if (num < 128)
                {
                    for (num++; num != 0; num--)
                    {
                        if (startIdx + i >= imgData.Length)
                        {
                            break;
                        }
                        b = (byte)stream.ReadByte();
                        imgData[startIdx + i] = b;
                        i++;
                    }
                }
                else if (num > 128)
                {
                    num ^= 255;
                    num += 2;
                    b = (byte)stream.ReadByte();
                    while (num != 0)
                    {
                        if (startIdx + i >= imgData.Length)
                        {
                            break;
                        }
                        imgData[startIdx + i] = b;
                        i++;
                        num--;
                    }
                }
            }
        }

        private class RlePacketStateMachine
        {
            internal void Flush()
            {
                byte value;
                if (_rlePacket)
                {
                    value = (byte)(-(byte)(_packetLength - 1));
                }
                else
                {
                    value = (byte)(_packetLength - 1);
                }
                _stream.WriteByte(value);
                var count = _rlePacket ? 1 : _packetLength;
                _stream.Write(_packetValues, 0, count);
                _packetLength = 0;
            }

            internal void Push(byte color)
            {
                var packetLength = _packetLength;
                if (packetLength == 0)
                {
                    _rlePacket = false;
                    _packetValues[0] = color;
                    _packetLength = 1;
                    return;
                }
                if (packetLength == 1)
                {
                    _rlePacket = (color == _packetValues[0]);
                    _packetValues[1] = color;
                    _packetLength = 2;
                    return;
                }
                if (_packetLength == _packetValues.Length)
                {
                    Flush();
                    Push(color);
                    return;
                }
                if (_packetLength >= 2 && _rlePacket && color != _packetValues[_packetLength - 1])
                {
                    Flush();
                    Push(color);
                    return;
                }
                if (_packetLength >= 2 && _rlePacket && color == _packetValues[_packetLength - 1])
                {
                    _packetLength++;
                    _packetValues[_packetLength - 1] = color;
                    return;
                }
                if (_packetLength >= 2 && !_rlePacket && color != _packetValues[_packetLength - 1])
                {
                    _packetLength++;
                    _packetValues[_packetLength - 1] = color;
                    return;
                }
                if (_packetLength >= 2 && !_rlePacket && color == _packetValues[_packetLength - 1])
                {
                    _packetLength--;
                    Flush();
                    Push(color);
                    Push(color);
                }
            }

            internal RlePacketStateMachine(Stream stream)
            {
                _stream = stream;
            }

            private bool _rlePacket;

            private readonly byte[] _packetValues = new byte[128];

            private int _packetLength;

            private readonly Stream _stream;
        }
    }
}