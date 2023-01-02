using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Platform.FileSystem;
using PixelMixel.Modules.PixelCanvas.Layers;
using Polenter.Serialization;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Plugins.Pxm
{
    public class PxmImporter : IImporter
    {
        public Task<IEnumerable<SKNode>> ImportFromFiles(IEnumerable<IFileContentSource> files)
        {
            throw new NotImplementedException();
        }

        public async Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget targetNode)
        {
            var file = files.FirstOrDefault();
            if (file == null)
                return;

            using (var stream = await file.OpenRead()) 
            {
                var artwork = await LoadFromStream(stream);

                var size = new SKSize(artwork.Size.Width, artwork.Size.Height);

                if (targetNode is SKNode node && node.Parent is IContainerNode artboard)
                {
                    ResizeContainer(artboard, size);
                }

                targetNode.SetSize(size);
                targetNode.Clear();

                InitLayers(targetNode, artwork);
                InitFrames(targetNode, artwork);

                LoadFrameBitmaps(targetNode, artwork, size);

                targetNode.SelectedLayerIndex = 0;
                targetNode.SetFrameIndex(0);

            }
        }

        private void LoadFrameBitmaps(IImportTarget targetNode, ArtWorkDefinition artwork, SKSize size)
        {
            var layerIndex = 0;
            foreach (var layer in artwork.Layers)
            {
                var frames = layer.Frames != null
                    ? GetFramesData(layer, artwork)
                    : new[] { layer.PixelsBytes };

                for (var frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    var frame = frames[frameIndex];
                    var bm = CreateBitmapFromBytes(frame, size);
                    targetNode.UpdateLayerFrameFromBitmap(frameIndex, layerIndex, bm);
                }

                layerIndex++;
            }
        }

        private byte[][] GetFramesData(PixelLayerDefinition layer, ArtWorkDefinition artwork)
        {
            if (artwork.Storyboard == null && artwork.Layers.Any())
            {
                //return GetLegacyFormatFramesData()
            }
            var layerAnim = artwork
                .Storyboard
                .Animations
                .OfType<LayerFrameAnimationDefinition>()
                .FirstOrDefault(x => x.LayerId == layer.Id);

            if (layerAnim == null)
                return null;

            var result = new byte[layerAnim.Keys.Length][];
            for (var i = 0; i < layerAnim.Keys.Length; i++)
            {
                var key = layerAnim.Keys[i];
                var frameId = key.Value;
                var frame = layer.Frames.FirstOrDefault(x => x.Id == frameId);
                if(frame != null)
                    result[i] = frame.PixelsBytes;
            }

            return result;
        }

        private void InitLayers(IImportTarget targetNode, ArtWorkDefinition artwork)
        {
            var layersCount = artwork.Layers.Length;
            for (var i = 0; i < layersCount; i++)
            {
                var layer = artwork.Layers[i];
                targetNode.AddLayer(new LayerPropertiesInfo()
                {
                    Opacity = layer.Opacity,
                    BlendMode = GetBlendMode(layer.BlendMode)
                });
            }
        }

        private SKBlendMode GetBlendMode(int? layerBlendMode)
        {
            switch (layerBlendMode)
            {
                case 0:
                    return SKBlendMode.Multiply;
                case 1:
                    return SKBlendMode.Screen;
                case 2:
                    return SKBlendMode.Darken;
                case 3:
                    return SKBlendMode.Lighten;
                case 5:
                    return SKBlendMode.ColorBurn;
                case 9:
                    return SKBlendMode.ColorDodge;
                case 11:
                    return SKBlendMode.Overlay;
                case 12:
                    return SKBlendMode.SoftLight;
                case 13:
                    return SKBlendMode.HardLight;
                case 19:
                    return SKBlendMode.Exclusion;
                case 20:
                    return SKBlendMode.Hue;
                case 21:
                    return SKBlendMode.Saturation;
                case 22:
                    return SKBlendMode.Color;
                case 23:
                    return SKBlendMode.Luminosity;
                default:
                    return SKBlendMode.SrcOver;
            }
        }

        private void InitFrames(IImportTarget targetNode, ArtWorkDefinition artwork)
        {
            var framesCount = artwork.Storyboard?.Animations?.OfType<LayerFrameAnimationDefinition>().FirstOrDefault()?.Keys?.Length ?? 1;
            if (framesCount == 1) return;
            
            for (int frameIndex = 1; frameIndex < framesCount; frameIndex++)
            {
                targetNode.AddEmptyFrame();
            }
        }

        private void ResizeContainer(IContainerNode artboard, SKSize size)
        {
            artboard.Resize(size, 0, 0);
        }

        private SKBitmap CreateBitmapFromBytes(byte[] bytes, SKSize size)
        {
            var bmInfo = new SKImageInfo((int)size.Width, (int)size.Height, SKColorType.Bgra8888);
            var bm = new SKBitmap(bmInfo);
            SetData(bm, bytes);
            return bm;
        }

        public void SetData(SKBitmap bitmap, byte[] data)
        {
            unsafe
            {
                fixed (byte* pSource = data)
                {
                    Buffer.MemoryCopy(pSource, bitmap.GetPixels().ToPointer(), data.Length, data.Length);
                }
            }
        }


        public async Task<ArtWorkDefinition> LoadFromStream(Stream stream, bool fromZip = true)
        {
            if (fromZip)
            {
                var zip = new ZipArchive(stream, ZipArchiveMode.Read);

                var prjEntry = zip.Entries.FirstOrDefault(x => x.Name == "project");
                if (prjEntry != null)
                {
                    return await ProjectFromZip(prjEntry);
                }
            }
            else
            {
                return await ProjectFromRaw(stream);
            }

            return null;
        }

        private Task<ArtWorkDefinition> ProjectFromZip(ZipArchiveEntry entry)
        {
            using (var entryStream = entry.Open())
            {
                var serializer = new SharpSerializer(new SharpSerializerBinarySettings(BinarySerializationMode.SizeOptimized)
                {
                    IncludeAssemblyVersionInTypeName = false,
                    IncludeCultureInTypeName = false,
                    IncludePublicKeyTokenInTypeName = false
                });
                var def = serializer.Deserialize(entryStream) as ProjectDefinition;

                foreach (var projectNodeDefinition in def.ProjectNodes)
                {
                    if (projectNodeDefinition is ArtWorkDefinition result)
                    {
                        return Task.FromResult(result);
                    }
                }
            }

            return Task.FromResult<ArtWorkDefinition>(default);
        }

        private Task<ArtWorkDefinition> ProjectFromRaw(Stream stream)
        {
            var serializer =
                new SharpSerializer(new SharpSerializerBinarySettings(BinarySerializationMode.SizeOptimized)
                {
                    IncludeAssemblyVersionInTypeName = false,
                    IncludeCultureInTypeName = false,
                    IncludePublicKeyTokenInTypeName = false
                });
            var def = serializer.Deserialize(stream) as ProjectDefinition;

            foreach (var projectNodeDefinition in def.ProjectNodes)
            {
                if (projectNodeDefinition is ArtWorkDefinition result)
                {
                    return Task.FromResult(result);
                }
            }

            return Task.FromResult<ArtWorkDefinition>(default);
        }

    }
}