namespace Pix2d.Plugins.Psd.PsdReader
{
    public class ImageResource
    {
        public short ID { get; set; }

        public string Name { get; }

        public byte[] Data { get; set; }

        public string OSType { get; private set; }

        public ImageResource()
        {
            OSType = string.Empty;
            Name = string.Empty;
        }

        public ImageResource(short id)
        {
            OSType = string.Empty;
            Name = string.Empty;
            ID = id;
        }

        public ImageResource(ImageResource imgRes)
        {
            OSType = string.Empty;
            ID = imgRes.ID;
            Name = imgRes.Name;
            Data = new byte[imgRes.Data.Length];
            imgRes.Data.CopyTo(Data, 0);
        }

        public ImageResource(BinaryReverseReader reverseReader)
        {
            Name = string.Empty;
            OSType = new string(reverseReader.ReadChars(4));
            if (OSType != "8BIM" && OSType != "MeSa")
            {
                throw new InvalidOperationException("Could not read an image resource");
            }
            ID = reverseReader.ReadInt16();
            Name = reverseReader.ReadPascalString();
            var count = reverseReader.ReadUInt32();
            Data = reverseReader.ReadBytes((int)count);
            if (reverseReader.BaseStream.Position % 2L == 1L)
            {
                reverseReader.ReadByte();
            }
        }

        //public void Save(BinaryReverseWriter reverseWriter)
        //{
        //    StoreData();
        //    if (OSType == string.Empty)
        //    {
        //        OSType = "8BIM";
        //    }
        //    reverseWriter.Write(OSType.ToCharArray());
        //    reverseWriter.Write(ID);
        //    reverseWriter.WritePascalString(Name);
        //    reverseWriter.Write(Data.Length);
        //    reverseWriter.Write(Data);
        //    if (reverseWriter.BaseStream.Position % 2L == 1L)
        //    {
        //        reverseWriter.Write(0);
        //    }
        //}

        //protected virtual void StoreData()
        //{
        //}

        public BinaryReverseReader DataReader => new(new MemoryStream(Data));

        public override string ToString()
        {
            return $"{(ResourceIDs)ID} {Name}";
        }
    }
}