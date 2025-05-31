using Pix2d.Plugins.SpinePlugin.Renderer;
using SkiaNodes;
using SkiaSharp;
using Spine;

namespace Pix2d.Plugins.SpinePlugin;

public class SpineNode : SKNode
{
    public Skeleton Skeleton { get; private set; }
    private Atlas _atlas;
    private SkeletonRenderer _skeletonRenderer;
    public string AtlasPath { get; set; }
    public string JsonPath { get; set; } 

    private Animation _anim;
    private float _time;
    private float _lastTime;

    public SpineNode()
    {
        LoadSkeleton();
    }
    public SpineNode(string jsonPath)
    {
        JsonPath = jsonPath;
        AtlasPath = jsonPath.Replace(".json", ".atlas");
        LoadSkeleton();
    }

    private void LoadSkeleton()
    {
        _atlas = new Atlas(AtlasPath, new SpineAtlasTextureLoader());
        
        var json = new SkeletonJson(_atlas) { Scale = 0.25f };
        var skeletonData = json.ReadSkeletonData(JsonPath);

        Skeleton = new Skeleton(skeletonData);
        Skeleton.SetToSetupPose();
        Skeleton.UpdateWorldTransform();

        _anim = Skeleton.Data.FindAnimation("attack");

        _skeletonRenderer = new(_atlas)
        {
            debugRendering = false,
            triangleRendering =  true
        };
    }

    public override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        canvas.Save();
        canvas.Scale(1, -1);
        DrawBoundingBox(canvas, vp, 3, SKColors.MediumPurple);
        DrawSkeleton(canvas, vp, Skeleton);
        canvas.Restore();
    }

    private void DrawSkeleton(SKCanvas canvas, ViewPort vp, Skeleton skeleton)
    {
        _time += 0.01f;
        if (_time > _anim.duration)
            _time = 0;

        Skeleton.SetToSetupPose();

        _anim.Apply(skeleton, _lastTime, _time, false, new ExposedList<Event>(), 1, MixBlend.Add, MixDirection.In);

        _lastTime = _time;
        Skeleton.UpdateWorldTransform();

        _skeletonRenderer.Draw(skeleton, canvas);
    }
}
