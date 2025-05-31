using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Tools;
using SkiaSharp;
using TiledSharp;

namespace Pix2d.Plugins.Tmx.Tools
{
    public class TileMapTool : ITool
    {
        public IFileService FileService { get; }
        public ISceneService SceneService { get; }
        public string Key => GetType().Name;
        public EditContextType EditContextType => EditContextType.General;
        public bool IsActive { get; private set; }
        public bool IsEnabled { get; set; } = true;
        public ToolBehaviorType Behavior => ToolBehaviorType.OneAction;
        public string NextToolKey { get; } = "";
        public string DisplayName => "Tile Map Tool";

        public string IconData =
            "M 1.5 3 C 1.222656 3 1 3.222656 1 3.5 L 1 5.5 C 1 5.777344 1.222656 6 1.5 6 L 4.5 6 C 4.777344 6 5 5.777344 5 5.5 L 5 3.5 C 5 3.222656 4.777344 3 4.5 3 Z M 6.5 3 C 6.222656 3 6 3.222656 6 3.5 L 6 5.5 C 6 5.777344 6.222656 6 6.5 6 L 9.5 6 C 9.777344 6 10 5.777344 10 5.5 L 10 3.5 C 10 3.222656 9.777344 3 9.5 3 Z M 11.5 3 C 11.222656 3 11 3.222656 11 3.5 L 11 5.5 C 11 5.777344 11.222656 6 11.5 6 L 14.5 6 C 14.777344 6 15 5.777344 15 5.5 L 15 3.5 C 15 3.222656 14.777344 3 14.5 3 Z M 2 4 L 4 4 L 4 5 L 2 5 Z M 7 4 L 9 4 L 9 5 L 7 5 Z M 12 4 L 14 4 L 14 5 L 12 5 Z M 1.5 7 C 1.222656 7 1 7.222656 1 7.5 L 1 9.5 C 1 9.777344 1.222656 10 1.5 10 L 4.5 10 C 4.777344 10 5 9.777344 5 9.5 L 5 7.5 C 5 7.222656 4.777344 7 4.5 7 Z M 6.5 7 C 6.222656 7 6 7.222656 6 7.5 L 6 9.5 C 6 9.777344 6.222656 10 6.5 10 L 9.5 10 C 9.777344 10 10 9.777344 10 9.5 L 10 7.5 C 10 7.222656 9.777344 7 9.5 7 Z M 11.5 7 C 11.222656 7 11 7.222656 11 7.5 L 11 9.5 C 11 9.777344 11.222656 10 11.5 10 L 14.5 10 C 14.777344 10 15 9.777344 15 9.5 L 15 7.5 C 15 7.222656 14.777344 7 14.5 7 Z M 2 8 L 4 8 L 4 9 L 2 9 Z M 7 8 L 9 8 L 9 9 L 7 9 Z M 12 8 L 14 8 L 14 9 L 12 9 Z M 1.5 11 C 1.222656 11 1 11.222656 1 11.5 L 1 13.5 C 1 13.777344 1.222656 14 1.5 14 L 4.5 14 C 4.777344 14 5 13.777344 5 13.5 L 5 11.5 C 5 11.222656 4.777344 11 4.5 11 Z M 6.5 11 C 6.222656 11 6 11.222656 6 11.5 L 6 13.5 C 6 13.777344 6.222656 14 6.5 14 L 9.5 14 C 9.777344 14 10 13.777344 10 13.5 L 10 11.5 C 10 11.222656 9.777344 11 9.5 11 Z M 11.5 11 C 11.222656 11 11 11.222656 11 11.5 L 11 13.5 C 11 13.777344 11.222656 14 11.5 14 L 14.5 14 C 14.777344 14 15 13.777344 15 13.5 L 15 11.5 C 15 11.222656 14.777344 11 14.5 11 Z M 2 12 L 4 12 L 4 13 L 2 13 Z M 7 12 L 9 12 L 9 13 L 7 13 Z M 12 12 L 14 12 L 14 13 L 12 13 Z ";

        public TileMapTool(IFileService fileService, ISceneService sceneService)
        {
            FileService = fileService;
            SceneService = sceneService;
        }

        public Task Activate()
        {
            ImportTmx();
            return Task.CompletedTask;
        }

        private async void ImportTmx()
        {
            var files = await FileService.OpenFileWithDialogAsync(new[] { ".tmx" }, true, "import_tmx");

            if (files == null || !files.Any())
                return;

            var file = files.First();

            var basePath = Path.GetDirectoryName(file.Path);

            var map = new TmxMap(await file.OpenRead());

            var version = map.Version;

            var tileSets = LoadTilesets(basePath, map.Tilesets);

            LoadLayers(map, tileSets);

            //var myTileset = map.Tilesets["myTileset"];
            //var myLayer = map.Layers[2];
            //var hiddenChest = map.ObjectGroups["Chests"].Objects["hiddenChest"];
        }

        private void LoadLayers(TmxMap map, TileSetCollection tileSets)
        {
            foreach (var mapLayer in map.TileLayers)
            {

                var layer = new TileMapNode();
                layer.Width = map.Width;
                layer.Height = map.Height;
                layer.TileWidth = map.TileWidth;
                layer.TileHeight = map.TileHeight;

                layer.TileSets = tileSets;

                layer.Name = mapLayer.Name;

                layer.Tiles = new int[layer.Width, layer.Height];
                foreach (var tile in mapLayer.Tiles)
                {
                    layer.Tiles[tile.X, tile.Y] = tile.Gid;
                }

                SceneService.GetCurrentScene().Nodes.Add(layer);
            }
        }

        private TileSetCollection LoadTilesets(string basePath, TmxList<TmxTileset> mapTilesets)
        {
            var result = new TileSetCollection();
            foreach (var mapTileset in mapTilesets)
            {
                var path = mapTileset.Image.Source;
                if (path.Contains("..") || path.Contains(".\\"))
                {
                    path = Path.Combine(basePath, path);
                }
                var bitmap = SKBitmap.Decode(path);

                result.Add(new TileSet()
                {
                    SourceBitmap = bitmap,
                    SourcePath = path,
                    FirstId = mapTileset.FirstGid,
                    TileCount = mapTileset.TileCount,
                    TileWidth = mapTileset.TileWidth,
                    TileHeight = mapTileset.TileHeight,
                    Columns = mapTileset.Columns
                });
            }

            return result;
        }

        public void Deactivate()
        {
        }

    }

    public class TileSet
    {
        public SKBitmap SourceBitmap { get; set; }
        public int FirstId { get; set; }
        public int? TileCount { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }

        public int? Columns { get; set; }

        public int Rows => TileCount.Value / Columns.Value;
        public string SourcePath { get; set; }
    }

    public class TileSetCollection
    {
        public List<TileSet> TileSets { get; set; } = new List<TileSet>();

        public void Add(TileSet tileSet)
        {
            TileSets.Add(tileSet);
        }

        public bool TryGetTileBitmap(int id, out SKBitmap bitmap, out SKRect rect)
        {
            var tileset = GetTilesetById(id);
            if (tileset == null)
            {
                rect = default;
                bitmap = default;

                return false;
            }

            var offset = id - tileset.FirstId;

            var row = 0;
            var col = 0;
            if (offset > 0)
            {
                row = offset / tileset.Rows;
                col = offset - row * tileset.Columns.Value;
            }

            var x = (float) (col * tileset.TileWidth);
            var y = row * tileset.TileHeight;
            rect = new SKRect(x, y, x + tileset.TileWidth, y + tileset.TileHeight);
            bitmap = tileset.SourceBitmap;

            return true;
        }

        public TileSet GetTilesetById(int id)
        {
            if (id == 0)
                return null;

            foreach (var tileSet in TileSets)
            {
                if (tileSet.FirstId <= id && tileSet.FirstId + tileSet.TileCount > id)
                    return tileSet;
            }

            return null;
        }

    }
}
