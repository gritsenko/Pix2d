using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pix2d.ViewModels.ToolSettings;
using SkiaNodes.Common;
using SkiaSharp;

namespace Pix2d.Exporters.AtlasPAcking
{
    public enum SplitType
    {
        Horizontal,
        Vertical,
    }

    public class AtlasNode
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public string TextureKey;
        public SplitType SplitType = SplitType.Horizontal;
        public int AtlasIndex;

        public AtlasNode()
        {
        }

        public AtlasNode(int size)
        {
            Width = size;
            Height = size;
        }
    }

    public class Atlas
    {
        public int Width;
        public int Height;
        public List<AtlasNode> Nodes = new List<AtlasNode>();
    }
    public class AtlasPacker
    {
        public int Size = 1024;
        public int Padding = 2;

        public List<Atlas> Atlasses;

        public Dictionary<string, SKBitmap> _sourceBitmaps = new Dictionary<string, SKBitmap>();

        public Dictionary<string, SKBitmap> SkippedTextures = new Dictionary<string, SKBitmap>();
        public void AddBitmap(string key, SKBitmap bm)
        {
            _sourceBitmaps[key] = bm;
        }

        public void BuildAtlases()
        {
            var textures = new List<string>();

            UpdateSkippedTextures();

            textures = _sourceBitmaps.Keys.ToList();
            Atlasses = new List<Atlas>();
            while (textures.Count > 0)
            {
                var atlas = new Atlas { Width = Size, Height = Size };
                LayoutAtlas(ref textures, atlas);
                Atlasses.Add(atlas);
            }

        }

        internal bool TryGetAtlasNode(string key, out AtlasNode node)
        {
            node = null;
            foreach (var atlass in Atlasses)
                foreach (var atlassNode in atlass.Nodes)
                    if (atlassNode.TextureKey == key)
                    {
                        node = atlassNode;
                        return true;
                    }

            return false;
        }

        private void UpdateSkippedTextures()
        {
            foreach (var sourceBitmap in _sourceBitmaps.ToArray())
            {
                if (sourceBitmap.Value.Width > Size || sourceBitmap.Value.Height > Size)
                {
                    SkippedTextures[sourceBitmap.Key] = sourceBitmap.Value;
                    _sourceBitmaps.Remove(sourceBitmap.Key);
                }
            }
        }

        private void LayoutAtlas(ref List<string> textures, Atlas atlas)
        {
            var root = new AtlasNode(Size);
            var freeNodes = new Queue<AtlasNode>(root.Yield());

            while (freeNodes.Count > 0 && textures.Count > 0)
            {
                var node = freeNodes.Dequeue();

                if (TryFindBestFitForNode(node, textures, out var bestFitKey))
                {
                    var bestFit = _sourceBitmaps[bestFitKey];

                    if (node.SplitType == SplitType.Horizontal)
                        HorizontalSplit(node, bestFit.Width, bestFit.Height, freeNodes);
                    else
                        VerticalSplit(node, bestFit.Width, bestFit.Height, freeNodes);

                    node.TextureKey = bestFitKey;
                    node.Width = bestFit.Width;
                    node.Height = bestFit.Height;
                    node.AtlasIndex = Atlasses.Count;

                    textures.Remove(bestFitKey);
                }

                atlas.Nodes.Add(node);
            }
        }

        private bool TryFindBestFitForNode(AtlasNode node, List<string> textures, out string bestFitKey)
        {
            bestFitKey = null;
            float nodeArea = node.Width * node.Height;
            float maxCriteria = 0.0f;

            foreach (var key in textures)
            {
                var ti = _sourceBitmaps[key];
                if (ti.Width <= node.Width && ti.Height <= node.Height)
                {
                    float textureArea = ti.Width * ti.Height;
                    float coverage = textureArea / nodeArea;
                    if (coverage > maxCriteria)
                    {
                        maxCriteria = coverage;
                        bestFitKey = key;
                    }
                }
            }

            return bestFitKey != null;
        }

        private void HorizontalSplit(AtlasNode toSplit, int width, int height, Queue<AtlasNode> freeNodes)
        {
            var n1 = new AtlasNode();
            n1.X = toSplit.X + width + Padding;
            n1.Y = toSplit.Y;
            n1.Width = toSplit.Width - width - Padding;
            n1.Height = height;
            n1.SplitType = SplitType.Vertical;

            var n2 = new AtlasNode();
            n2.X = toSplit.X;
            n2.Y = toSplit.Y + height + Padding;
            n2.Width = toSplit.Width;
            n2.Height = toSplit.Height - height - Padding;
            n2.SplitType = SplitType.Horizontal;

            if (n1.Width > 0 && n1.Height > 0)
                freeNodes.Enqueue(n1);
            if (n2.Width > 0 && n2.Height > 0)
                freeNodes.Enqueue(n2);
        }

        private void VerticalSplit(AtlasNode toSplit, int width, int height, Queue<AtlasNode> freeNodes)
        {
            var n1 = new AtlasNode();
            n1.X = toSplit.X + width + Padding;
            n1.Y = toSplit.Y;
            n1.Width = toSplit.Width - width - Padding;
            n1.Height = toSplit.Height;
            n1.SplitType = SplitType.Vertical;

            var n2 = new AtlasNode();
            n2.X = toSplit.X;
            n2.Y = toSplit.Y + height + Padding;
            n2.Width = width;
            n2.Height = toSplit.Height - height - Padding;
            n2.SplitType = SplitType.Horizontal;

            if (n1.Width > 0 && n1.Height > 0)
                freeNodes.Enqueue(n1);
            if (n2.Width > 0 && n2.Height > 0)
                freeNodes.Enqueue(n2);
        }




    }
}
