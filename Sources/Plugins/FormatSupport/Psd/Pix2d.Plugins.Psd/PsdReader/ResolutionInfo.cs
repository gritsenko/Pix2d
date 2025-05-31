namespace Pix2d.Plugins.Psd.PsdReader
{
    public class ResolutionInfo : ImageResource
    {
        public short HRes { get; private set; }

        public short VRes { get; private set; }

        public ResUnit HResUnit { get; private set; }

        public ResUnit VResUnit { get; private set; }

        public Unit WidthUnit { get; private set; }

        public Unit HeightUnit { get; private set; }

        //public ResolutionInfo()
        //{
        //    ID = 1005;
        //}

        public ResolutionInfo(ImageResource imgRes) : base(imgRes)
        {
            var dataReader = imgRes.DataReader;
            HRes = dataReader.ReadInt16();
            HResUnit = (ResUnit)dataReader.ReadInt32();
            WidthUnit = (Unit)dataReader.ReadInt16();
            VRes = dataReader.ReadInt16();
            VResUnit = (ResUnit)dataReader.ReadInt32();
            HeightUnit = (Unit)dataReader.ReadInt16();
            dataReader.Close();
        }

        //protected override void StoreData()
        //{
        //    var memoryStream = new MemoryStream();
        //    var binaryReverseWriter = new BinaryReverseWriter(memoryStream);
        //    binaryReverseWriter.Write(HRes);
        //    binaryReverseWriter.Write((int)HResUnit);
        //    binaryReverseWriter.Write((short)WidthUnit);
        //    binaryReverseWriter.Write(VRes);
        //    binaryReverseWriter.Write((int)VResUnit);
        //    binaryReverseWriter.Write((short)HeightUnit);
        //    binaryReverseWriter.Close();
        //    memoryStream.Close();
        //    Data = memoryStream.ToArray();
        //}

        public override string ToString()
        {
            return string.Format("{0}{2}x{1}{3}", new object[]
            {
                HRes,
                VRes,
                Enum.GetName(typeof(Unit), WidthUnit),
                Enum.GetName(typeof(Unit), HeightUnit)
            });
        }

        public enum ResUnit
        {
            PxPerInch = 1,
            PxPerCent
        }

        public enum Unit
        {
            In = 1,
            Cm,
            Pt,
            Picas,
            Columns
        }
    }
}