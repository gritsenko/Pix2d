using System;
using System.Collections.Generic;
using System.Text;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using System.Diagnostics.CodeAnalysis;
using Pix2d.Plugins.Tmx.Tools;

namespace Pix2d.Plugins.Tmx
{
    [method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(TileMapPlugin))]
    public class TileMapPlugin : IPix2dPlugin
    {
        private TileMapTool _tileMapTool;
        public IToolService ToolService { get; }
        public IFileService FileService { get; }
        public ISceneService SceneService { get; }

        public TileMapPlugin(IToolService toolService, IFileService fileService, ISceneService sceneService)
        {
            ToolService = toolService;
            FileService = fileService;
            SceneService = sceneService;

            _tileMapTool = new TileMapTool(FileService, SceneService);
        }
        public void Initialize()
        {
            ToolService.RegisterTool(_tileMapTool);
        }
    }
}
