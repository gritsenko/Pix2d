using System.Collections.Generic;
using Pix2d.Abstract;

namespace Pix2d.Services
{
    public class AssetStorageService : IAssetStorageService
    {
        public Dictionary<string, AssetItem> Assets { get; } = new Dictionary<string, AssetItem>();

        public void UpdateFromDirectory()
        {

        }
    }

    public class AssetItem
    {
        public Dictionary<string, AssetItem> Assets { get; } = new Dictionary<string, AssetItem>();


        public string Name { get; set; }

        public string Path { get; set; }
    }
}