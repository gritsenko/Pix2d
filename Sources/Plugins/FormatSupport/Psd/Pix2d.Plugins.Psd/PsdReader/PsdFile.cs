namespace Pix2d.Plugins.Psd.PsdReader
{
    public class PsdFile
    {
        public short Version { get; private set; }

        public short Channels
        {
            get => _channels;
            private set
            {
                if (value < 1 || value > 24)
                {
                    throw new ArgumentException("Supported range is 1 to 24");
                }
                _channels = value;
            }
        }

        public int Rows
        {
            get => _rows;
            private set
            {
                if (value < 0 || value > 30000)
                {
                    throw new ArgumentException("Supported range is 1 to 30000.");
                }
                _rows = value;
            }
        }

        public int Columns
        {
            get => _columns;
            private set
            {
                if (value < 0 || value > 30000)
                {
                    throw new ArgumentException("Supported range is 1 to 30000.");
                }
                _columns = value;
            }
        }

        public int Depth
        {
            get => _depth;
            private set
            {
                if (value == 1 || value == 8 || value == 16)
                {
                    _depth = value;
                    return;
                }
                throw new ArgumentException("Supported values are 1, 8, and 16.");
            }
        }

        public ColorModes ColorMode { get; private set; }

        public IEnumerable<Layer> Layers => _layers;

        public bool AbsoluteAlpha { get; private set; }

        public byte[][] ImageData { get; private set; }

        public ImageCompression ImageCompression { get; private set; }

        public IEnumerable<ImageResource> ImageResources => _imageResources;

        public ResolutionInfo Resolution
        {
            get { return (ResolutionInfo) _imageResources.Find((ImageResource x) => x.ID == 1005); }
            private set
            {
                var imageResource = _imageResources.Find((ImageResource x) => x.ID == 1005);
                if (imageResource != null)
                {
                    _imageResources.Remove(imageResource);
                }
                _imageResources.Add(value);
            }
        }

        public PsdFile()
        {
            _layers = new List<Layer>();
            Version = 1;
            _imageResources = new List<ImageResource>();
        }

        public PsdFile Load(string filename)
        {
            PsdFile result;
            using (var fileStream = new FileStream(filename, FileMode.Open))
            {
                result = Load(fileStream);
            }
            return result;
        }

        public PsdFile Load(byte[] data)
        {
            var stream = new MemoryStream(data);
            return Load(stream);
        }

        public PsdFile Load(Stream stream)
        {
            var binaryReverseReader = new BinaryReverseReader(stream);
            if (new string(binaryReverseReader.ReadChars(4)) != "8BPS")
            {
                throw new IOException("Bad or invalid file stream supplied");
            }
            if ((Version = binaryReverseReader.ReadInt16()) != 1)
            {
                throw new IOException("Invalid version number supplied");
            }
            binaryReverseReader.BaseStream.Position += 6L;
            _channels = binaryReverseReader.ReadInt16();
            _rows = binaryReverseReader.ReadInt32();
            _columns = binaryReverseReader.ReadInt32();
            _depth = binaryReverseReader.ReadInt16();
            ColorMode = (ColorModes) binaryReverseReader.ReadInt16();
            var num = binaryReverseReader.ReadUInt32();
            if (num > 0u)
            {
                ColorModeData = binaryReverseReader.ReadBytes((int) num);
            }
            _imageResources.Clear();
            var num2 = binaryReverseReader.ReadUInt32();
            if (num2 <= 0u)
            {
                return null;
            }
            var position = binaryReverseReader.BaseStream.Position;
            while (binaryReverseReader.BaseStream.Position - position < num2)
            {
                var imageResource = new ImageResource(binaryReverseReader);
                var id = (ResourceIDs) imageResource.ID;
                if (id <= ResourceIDs.AlphaChannelNames)
                {
                    if (id != ResourceIDs.ResolutionInfo)
                    {
                        if (id == ResourceIDs.AlphaChannelNames)
                        {
                            imageResource = new AlphaChannels(imageResource);
                        }
                    }
                    else
                    {
                        imageResource = new ResolutionInfo(imageResource);
                    }
                }
                else if (id == ResourceIDs.Thumbnail1 || id == ResourceIDs.Thumbnail2)
                {
                    imageResource = new Thumbnail(imageResource);
                }
                _imageResources.Add(imageResource);
            }
            binaryReverseReader.BaseStream.Position = position + num2;
            var num3 = binaryReverseReader.ReadUInt32();
            if (num3 <= 0u)
            {
                return null;
            }
            position = binaryReverseReader.BaseStream.Position;
            LoadLayers(binaryReverseReader);
            LoadGlobalLayerMask(binaryReverseReader);
            binaryReverseReader.BaseStream.Position = position + num3;
            ImageCompression = (ImageCompression) binaryReverseReader.ReadInt16();
            ImageData = new byte[_channels][];
            if (ImageCompression == ImageCompression.Rle)
            {
                binaryReverseReader.BaseStream.Position += _rows * _channels * 2;
            }
            var num4 = 0;
            var depth = _depth;
            if (depth != 1)
            {
                if (depth != 8)
                {
                    if (depth == 16)
                    {
                        num4 = _columns * 2;
                    }
                }
                else
                {
                    num4 = _columns;
                }
            }
            else
            {
                num4 = _columns;
            }
            for (var i = 0; i < (int) _channels; i++)
            {
                ImageData[i] = new byte[_rows * num4];
                var imageCompression = ImageCompression;
                if (imageCompression != ImageCompression.Raw)
                {
                    if (imageCompression == ImageCompression.Rle)
                    {
                        for (var j = 0; j < _rows; j++)
                        {
                            var startIdx = j * _columns;
                            RleHelper.DecodedRow(binaryReverseReader.BaseStream, ImageData[i], startIdx, num4);
                        }
                    }
                }
                else
                {
                    binaryReverseReader.Read(ImageData[i], 0, ImageData[i].Length);
                }
            }
            return this;
        }

        private void LoadLayers(BinaryReverseReader reader)
        {
            var num = reader.ReadUInt32();
            if (num <= 0u)
            {
                return;
            }
            var position = reader.BaseStream.Position;
            var num2 = reader.ReadInt16();
            if (num2 < 0)
            {
                AbsoluteAlpha = true;
                num2 = Math.Abs(num2);
            }
            _layers.Clear();
            if (num2 == 0)
            {
                return;
            }
            for (var i = 0; i < (int) num2; i++)
            {
                _layers.Add(new Layer(reader, this));
            }
            foreach (var layer in Layers)
            {
                foreach (var channel in from c in layer.Channels
                    where c.ID != -2
                    select c)
                {
                    channel.LoadPixelData(reader);
                }
                layer.MaskData.LoadPixelData(reader);
            }
            if (reader.BaseStream.Position % 2L == 1L)
            {
                reader.ReadByte();
            }
            reader.BaseStream.Position = position + num;
        }

        private void LoadGlobalLayerMask(BinaryReverseReader reader)
        {
            var num = reader.ReadUInt32();
            if (num <= 0u)
            {
                return;
            }
            _globalLayerMaskData = reader.ReadBytes((int) num);
        }

        public byte[] ColorModeData = new byte[0];

        private byte[] _globalLayerMaskData = new byte[0];

        private short _channels;

        private int _rows;

        private int _columns;

        private int _depth;

        private List<Layer> _layers;

        private List<ImageResource> _imageResources;

        public enum ColorModes
        {
            Bitmap,
            Grayscale,
            Indexed,
            RGB,
            CMYK,
            Multichannel = 7,
            Duotone,
            Lab
        }
    }
}