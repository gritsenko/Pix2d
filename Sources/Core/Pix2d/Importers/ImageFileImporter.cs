using System;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaNodes.Abstract;
using SkiaNodes.Extensions;

namespace Pix2d.Importers
{
    public class ImageFileImporter : IImporter
    {
        private IObjectCreationService objectCreationService { get; set; }

        public ImageFileImporter(IObjectCreationService objectCreationService)
        {
            this.ObjectCreationService = objectCreationService;
        }

        public IObjectCreationService ObjectCreationService { get => objectCreationService; set => objectCreationService = value; }


        public async Task<IEnumerable<SKNode>> ImportFromFiles(IEnumerable<IFileContentSource> files)
        {
            var fileArr = files.Where(x => IsImageSupported(x.Extension)).ToArray();

            SKNode container = null;
            if (fileArr.Length > 1)
            {
                throw new NotImplementedException();
                //container = new AnimatedSprite() { Name = "AnimatedSprite" };
            }

            var result = new List<SKNode>();
            foreach (var file in fileArr)
            {
                if (file != null)
                {
                    using (var stream = await file.OpenRead())
                    {
                        var bm = stream.ToSKBitmap();

                        var sprite = ObjectCreationService.CreateSprite(bm, container);
                        sprite.Name = System.IO.Path.GetFileNameWithoutExtension(file.Path);
                        var textureKey = System.IO.Path.GetFileNameWithoutExtension(sprite.Name).Replace("_", "").Replace(" ", "");
                        sprite.DesignerState.ExportSettings.TextureKey = textureKey;

                        result.Add(sprite);
                    }
                }
            }

            //if (container != null)
            //{
            //    var animService = ServiceLocator.Current.GetInstance<IAnimationService>();
            //    animService.AddPreviewNode(container);
            //}

            return container?.Yield() ?? result;
        }

        public async Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget targetNode)
        {
            var frames = new List<SKBitmap>();
            int w = 0, h = 0;
            foreach (var file in files)
                using (var stream = await file.OpenRead())
                {
                    var bm = stream.ToSKBitmap();
                    frames.Add(bm);

                    w = Math.Max(w, bm.Width);
                    h = Math.Max(h, bm.Height);
                }

            var size = new SKSize(w, h);

            if (targetNode is SKNode node && node.Parent is IContainerNode containerNode)
            {
                ResizeContainer(containerNode, size);
            }

            targetNode.Clear();
            targetNode.SetSize(size);

            for (var i = 0; i < frames.Count; i++)
            {
                targetNode.AddEmptyFrame(size);
                targetNode.UpdateLayerFrameFromBitmap(i, 0, frames[i]);
            }
        }

        private void ResizeContainer(IContainerNode artboard, SKSize size)
        {
            artboard.Resize(size, 0, 0);
        }

        private bool IsImageSupported(string path)
        {
            if (path.ToLower().EndsWith(".png")) return true;
            if (path.ToLower().EndsWith(".jpg")) return true;
            if (path.ToLower().EndsWith(".jpeg")) return true;

            return false;
        }
    }
}
