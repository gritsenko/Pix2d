using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.SpinePlugin
{
    public class SpineImporter : IImporter
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

            var spineNode = new SpineNode(file.Path);

            if (targetNode is SKNode node)
            {
                spineNode.Size = new SKSize(200, 200);
                node.Parent.Nodes.Add(spineNode);
            }
        }
    }
}