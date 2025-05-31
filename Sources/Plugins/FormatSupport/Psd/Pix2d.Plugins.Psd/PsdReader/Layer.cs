using System.Collections.Specialized;
using System.Drawing;

namespace Pix2d.Plugins.Psd.PsdReader
{
    public class Layer
    {
        internal PsdFile PsdFile { get; private set; }

        public Rectangle Rect { get; private set; }

        public List<Channel> Channels { get; private set; }

        public SortedList<short, Channel> SortedChannels { get; private set; }

        public string BlendModeKey
        {
            get => _blendModeKeyStr;
            private set
            {
                if (value.Length != 4)
                {
                    throw new ArgumentException("Key length must be 4");
                }
                _blendModeKeyStr = value;
            }
        }

        public byte Opacity { get; private set; }

        public bool Clipping { get; private set; }

        public bool Visible
        {
            get => !_flags[VisibleBit];
            private set => _flags[VisibleBit] = !value;
        }

        public bool ProtectTrans
        {
            get => _flags[ProtectTransBit];
            private set => _flags[ProtectTransBit] = value;
        }

        public string Name { get; private set; }

        public BlendingRanges BlendingRangesData { get; set; }

        public Mask MaskData { get; private set; }

        public List<AdjusmentLayerInfo> AdjustmentInfo { get; private set; }

        public Layer(PsdFile psdFile)
        {
            AdjustmentInfo = new List<AdjusmentLayerInfo>();
            SortedChannels = new SortedList<short, Channel>();
            Channels = new List<Channel>();
            Rect = Rectangle.Empty;
            PsdFile = psdFile;
        }

        public Layer(BinaryReverseReader reverseReader, PsdFile psdFile)
        {
            AdjustmentInfo = new List<AdjusmentLayerInfo>();
            SortedChannels = new SortedList<short, Channel>();
            Channels = new List<Channel>();
            PsdFile = psdFile;
            var rect = new Rectangle
            {
                Y = reverseReader.ReadInt32(),
                X = reverseReader.ReadInt32()
            };
            rect.Height = reverseReader.ReadInt32() - rect.Y;
            rect.Width = reverseReader.ReadInt32() - rect.X;
            Rect = rect;
            var num = (int) reverseReader.ReadUInt16();
            Channels.Clear();
            for (var i = 0; i < num; i++)
            {
                var channel = new Channel(reverseReader, this);
                Channels.Add(channel);
                SortedChannels.Add(channel.ID, channel);
            }
            if (new string(reverseReader.ReadChars(4)) != "8BIM")
            {
                throw new IOException("Layer Channelheader error");
            }
            _blendModeKeyStr = new string(reverseReader.ReadChars(4));
            Opacity = reverseReader.ReadByte();
            Clipping = (reverseReader.ReadByte() > 0);
            var data = reverseReader.ReadByte();
            _flags = new BitVector32(data);
            reverseReader.ReadByte();
            var num2 = reverseReader.ReadUInt32();
            var position = reverseReader.BaseStream.Position;
            MaskData = new Mask(reverseReader, this);
            BlendingRangesData = new BlendingRanges(reverseReader, this);
            var position2 = reverseReader.BaseStream.Position;
            Name = reverseReader.ReadPascalString();
            var count = (int) ((reverseReader.BaseStream.Position - position2) % 4L);
            reverseReader.ReadBytes(count);
            AdjustmentInfo.Clear();
            var num3 = position + num2;
            while (reverseReader.BaseStream.Position < num3)
            {
                try
                {
                    AdjustmentInfo.Add(new AdjusmentLayerInfo(reverseReader, this));
                }
                catch
                {
                    reverseReader.BaseStream.Position = num3;
                }
            }
            reverseReader.BaseStream.Position = num3;
        }

        public void Save(BinaryReverseWriter reverseWriter)
        {
            reverseWriter.Write(Rect.Top);
            reverseWriter.Write(Rect.Left);
            reverseWriter.Write(Rect.Bottom);
            reverseWriter.Write(Rect.Right);
            reverseWriter.Write((short) Channels.Count);
            foreach (var channel in Channels)
            {
                channel.Save(reverseWriter);
            }
            reverseWriter.Write("8BIM".ToCharArray());
            reverseWriter.Write(_blendModeKeyStr.ToCharArray());
            reverseWriter.Write(Opacity);
            reverseWriter.Write(Clipping ? 1 : 0);
            reverseWriter.Write((byte) _flags.Data);
            reverseWriter.Write(0);
            using (new LengthWriter(reverseWriter))
            {
                MaskData.Save(reverseWriter);
                BlendingRangesData.Save(reverseWriter);
                var position = reverseWriter.BaseStream.Position;
                reverseWriter.WritePascalString(Name);
                var num = (int) ((reverseWriter.BaseStream.Position - position) % 4L);
                for (var i = 0; i < num; i++)
                {
                    reverseWriter.Write(0);
                }
                foreach (var adjusmentLayerInfo in AdjustmentInfo)
                {
                    adjusmentLayerInfo.Save(reverseWriter);
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", Name, Visible ? "Visible" : "Hidden", BlendModeKey);
        }

        private string _blendModeKeyStr = "norm";

        private static readonly int ProtectTransBit = BitVector32.CreateMask();

        private static readonly int VisibleBit = BitVector32.CreateMask(ProtectTransBit);

        private BitVector32 _flags;

        public class AdjusmentLayerInfo
        {
            private Layer Layer { get; set; }

            public string Key { get; private set; }

            public byte[] Data { get; private set; }

            public AdjusmentLayerInfo(string key, Layer layer)
            {
                Key = key;
                Layer = layer;
                Layer.AdjustmentInfo.Add(this);
            }

            public AdjusmentLayerInfo(BinaryReverseReader reader, Layer layer)
            {
                Layer = layer;
                if (new string(reader.ReadChars(4)) != "8BIM")
                {
                    throw new IOException("Could not read an image resource");
                }
                Key = new string(reader.ReadChars(4));
                var count = reader.ReadUInt32();
                Data = reader.ReadBytes((int) count);
            }

            public void Save(BinaryReverseWriter writer)
            {
                writer.Write("8BIM".ToCharArray());
                writer.Write(Key.ToCharArray());
                writer.Write((uint) Data.Length);
                writer.Write(Data);
            }

            public BinaryReverseReader DataReader => new BinaryReverseReader(new MemoryStream(Data));
        }

        public class BlendingRanges
        {
            public Layer Layer { get; private set; }

            public byte[] Data { get; private set; }

            public BlendingRanges(Layer layer)
            {
                Data = new byte[0];
                Layer = layer;
                Layer.BlendingRangesData = this;
            }

            public BlendingRanges(BinaryReverseReader reader, Layer layer)
            {
                Data = new byte[0];
                Layer = layer;
                var num = reader.ReadInt32();
                if (num <= 0)
                {
                    return;
                }
                Data = reader.ReadBytes(num);
            }

            public void Save(BinaryReverseWriter writer)
            {
                writer.Write((uint) Data.Length);
                writer.Write(Data);
            }
        }

        public class Channel
        {
            public Layer Layer { get; private set; }

            public short ID { get; private set; }

            public int Length { get; private set; }

            public byte[] Data { get; set; }

            public byte[] ImageData { get; set; }

            public ImageCompression ImageCompression { get; set; }

            internal Channel(short id, Layer layer)
            {
                ID = id;
                Layer = layer;
                Layer.Channels.Add(this);
                Layer.SortedChannels.Add(ID, this);
            }

            internal Channel(BinaryReverseReader reverseReader, Layer layer)
            {
                ID = reverseReader.ReadInt16();
                Length = reverseReader.ReadInt32();
                Layer = layer;
            }

            internal void Save(BinaryReverseWriter reverseWriter)
            {
                reverseWriter.Write(ID);
                CompressImageData();
                reverseWriter.Write(Data.Length + 2);
            }

            internal void LoadPixelData(BinaryReverseReader reverseReader)
            {
                Data = reverseReader.ReadBytes(Length);
                using (var dataReader = DataReader)
                {
                    ImageCompression = (ImageCompression) dataReader.ReadInt16();
                    var num = 0;
                    var depth = Layer.PsdFile.Depth;
                    if (depth != 1)
                    {
                        if (depth != 8)
                        {
                            if (depth == 16)
                            {
                                num = Layer.Rect.Width * 2;
                            }
                        }
                        else
                        {
                            num = Layer.Rect.Width;
                        }
                    }
                    else
                    {
                        num = Layer.Rect.Width;
                    }
                    ImageData = new byte[Layer.Rect.Height * num];
                    var imageCompression = ImageCompression;
                    if (imageCompression != ImageCompression.Raw)
                    {
                        if (imageCompression == ImageCompression.Rle)
                        {
                            var array = new int[Layer.Rect.Height];
                            for (var i = 0; i < array.Length; i++)
                            {
                                array[i] = dataReader.ReadInt16();
                            }
                            for (var j = 0; j < Layer.Rect.Height; j++)
                            {
                                var startIdx = j * Layer.Rect.Width;
                                RleHelper.DecodedRow(dataReader.BaseStream, ImageData, startIdx, num);
                            }
                        }
                    }
                    else
                    {
                        dataReader.Read(ImageData, 0, ImageData.Length);
                    }
                }
            }

            private void CompressImageData()
            {
                if (ImageCompression == ImageCompression.Rle)
                {
                    var memoryStream = new MemoryStream();
                    var binaryReverseWriter = new BinaryReverseWriter(memoryStream);
                    var position = binaryReverseWriter.BaseStream.Position;
                    var array = new int[Layer.Rect.Height];
                    if (ImageCompression == ImageCompression.Rle)
                    {
                        for (var i = 0; i < array.Length; i++)
                        {
                            binaryReverseWriter.Write(4660);
                        }
                    }
                    var columns = 0;
                    var j = Layer.PsdFile.Depth;
                    if (j != 1)
                    {
                        if (j != 8)
                        {
                            if (j == 16)
                            {
                                columns = Layer.Rect.Width * 2;
                            }
                        }
                        else
                        {
                            columns = Layer.Rect.Width;
                        }
                    }
                    else
                    {
                        columns = Layer.Rect.Width;
                    }
                    for (var k = 0; k < Layer.Rect.Height; k++)
                    {
                        var startIdx = k * Layer.Rect.Width;
                        array[k] = RleHelper.EncodedRow(binaryReverseWriter.BaseStream, ImageData, startIdx, columns);
                    }
                    var position2 = binaryReverseWriter.BaseStream.Position;
                    binaryReverseWriter.BaseStream.Position = position;
                    foreach (var num in array)
                    {
                        binaryReverseWriter.Write((short) num);
                    }
                    binaryReverseWriter.BaseStream.Position = position2;
                    memoryStream.Close();
                    Data = memoryStream.ToArray();
                    memoryStream.Dispose();
                    return;
                }
                Data = (byte[]) ImageData.Clone();
            }

            internal void SavePixelData(BinaryReverseWriter writer)
            {
                writer.Write((short) ImageCompression);
                writer.Write(ImageData);
            }

            public BinaryReverseReader DataReader
            {
                get
                {
                    if (Data != null)
                    {
                        return new BinaryReverseReader(new MemoryStream(Data));
                    }
                    return null;
                }
            }
        }

        public class Mask
        {
            public Layer Layer { get; private set; }

            public Rectangle Rect { get; private set; }

            public byte DefaultColor { get; private set; }

            public bool PositionIsRelative
            {
                get => _flags[PositionIsRelativeBit];
                private set => _flags[PositionIsRelativeBit] = value;
            }

            public bool Disabled
            {
                get => _flags[DisabledBit];
                private set => _flags[DisabledBit] = value;
            }

            public bool InvertOnBlendBit
            {
                get => _flags[_invertOnBlendBit];
                private set => _flags[_invertOnBlendBit] = value;
            }

            internal Mask(Layer layer)
            {
                Layer = layer;
                Layer.MaskData = this;
            }

            internal Mask(BinaryReverseReader reader, Layer layer)
            {
                Layer = layer;
                var num = reader.ReadUInt32();
                if (num <= 0u)
                {
                    return;
                }
                var position = reader.BaseStream.Position;
                var rect = new Rectangle
                {
                    Y = reader.ReadInt32(),
                    X = reader.ReadInt32()
                };
                rect.Height = reader.ReadInt32() - rect.Y;
                rect.Width = reader.ReadInt32() - rect.X;
                Rect = rect;
                DefaultColor = reader.ReadByte();
                var data = reader.ReadByte();
                _flags = new BitVector32(data);
                if (num == 36u)
                {
                    new BitVector32(reader.ReadByte());
                    reader.ReadByte();
                    var rectangle = default(Rectangle);
                    rectangle.Y = reader.ReadInt32();
                    rectangle.X = reader.ReadInt32();
                    rectangle.Height = reader.ReadInt32() - Rect.Y;
                    rectangle.Width = reader.ReadInt32() - Rect.X;
                }
                reader.BaseStream.Position = position + num;
            }

            public void Save(BinaryReverseWriter writer)
            {
                if (Rect.IsEmpty)
                {
                    writer.Write(0u);
                    return;
                }
                using (new LengthWriter(writer))
                {
                    writer.Write(Rect.Top);
                    writer.Write(Rect.Left);
                    writer.Write(Rect.Bottom);
                    writer.Write(Rect.Right);
                    writer.Write(DefaultColor);
                    writer.Write((byte) _flags.Data);
                    writer.Write(0);
                }
            }

            public byte[] ImageData { get; set; }

            internal void LoadPixelData(BinaryReverseReader reader)
            {
                if (Rect.IsEmpty || !Layer.SortedChannels.ContainsKey(-2))
                {
                    return;
                }
                var channel = Layer.SortedChannels[-2];
                channel.Data = reader.ReadBytes(channel.Length);
                using (var dataReader = channel.DataReader)
                {
                    channel.ImageCompression = (ImageCompression) dataReader.ReadInt16();
                    var num = 0;
                    var depth = Layer.PsdFile.Depth;
                    if (depth != 1)
                    {
                        if (depth != 8)
                        {
                            if (depth == 16)
                            {
                                num = Rect.Width * 2;
                            }
                        }
                        else
                        {
                            num = Rect.Width;
                        }
                    }
                    else
                    {
                        num = Rect.Width;
                    }
                    channel.ImageData = new byte[Rect.Height * num];
                    for (var i = 0; i < channel.ImageData.Length; i++)
                    {
                        channel.ImageData[i] = 171;
                    }
                    ImageData = (byte[]) channel.ImageData.Clone();
                    var imageCompression = channel.ImageCompression;
                    if (imageCompression != ImageCompression.Raw)
                    {
                        if (imageCompression == ImageCompression.Rle)
                        {
                            var array = new int[Rect.Height];
                            for (var j = 0; j < array.Length; j++)
                            {
                                array[j] = dataReader.ReadInt16();
                            }
                            for (var k = 0; k < Rect.Height; k++)
                            {
                                var startIdx = k * Rect.Width;
                                RleHelper.DecodedRow(dataReader.BaseStream, channel.ImageData, startIdx, num);
                            }
                        }
                    }
                    else
                    {
                        dataReader.Read(channel.ImageData, 0, channel.ImageData.Length);
                    }
                    ImageData = (byte[]) channel.ImageData.Clone();
                }
            }

            internal void SavePixelData(BinaryReverseWriter writer)
            {
            }

            private static readonly int PositionIsRelativeBit = BitVector32.CreateMask();

            private static readonly int DisabledBit = BitVector32.CreateMask(PositionIsRelativeBit);

            private static readonly int _invertOnBlendBit = BitVector32.CreateMask(DisabledBit);

            private BitVector32 _flags;
        }
    }
}