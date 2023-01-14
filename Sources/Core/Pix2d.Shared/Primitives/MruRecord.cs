using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Primitives
{
    public class MruRecord
    {
        public string Name { get; set; }
        public string Path { get; set; }

        public MruRecord()
        {
        }

        public MruRecord(IFileContentSource file)
        {
            Path = file.Path;
            Name = file.Title;
        }

        protected bool Equals(MruRecord other)
        {
            return Name == other.Name && Path == other.Path;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MruRecord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Path != null ? Path.GetHashCode() : 0);
            }
        }
    }
}