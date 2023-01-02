using SkiaNodes.Extensions;
using SkiaSharp;
using Spine;
using System;
using System.Drawing;

namespace Pix2d.Plugins.SpinePlugin.Renderer;

internal class SkeletonRenderer
{
    static readonly float[] worldVertices = new float[8];

    static int[] QUAD_TRIANGLES = { 0, 1, 2, 2, 3, 0 };
    static int VERTEX_SIZE = 2 + 2 + 4;

    public bool triangleRendering = false;
    public bool debugRendering = false;
    private float[] vertices = new float[8 * 1024];
    private SpineColor tempColor = new();

    private SKPaint _debugPaint;

    private Atlas _atlas;

    public SkeletonRenderer(Atlas atlas)
    {
        _atlas = atlas;
        _debugPaint = new SKPaint()
        {
            StrokeWidth = 0.5f,
            Color = SKColors.Green,
            IsStroke = true
        };
    }

    public void Draw(Skeleton skeleton, SKCanvas canvas)
    {
        if (triangleRendering) DrawTriangles(skeleton, canvas);
        else DrawImages(skeleton, canvas);
    }

    private void DrawImages(Skeleton skeleton, SKCanvas canvas)
    {
        var ctx = canvas;
        var color = tempColor;
        var skeletonColor = SpineColor.Get(skeleton);
        var drawOrder = skeleton.drawOrder;

        //if (this.debugRendering) ctx.strokeStyle = "green";

        for (int i = 0, n = drawOrder.Count; i < n; i++)
        {
            var slot = drawOrder.Items[i];
            var bone = slot.bone;
            if (!bone.active)
                continue;

            var attachment = slot.Attachment;
            AtlasRegion region = null;
            if (attachment is RegionAttachment regionAttachment)
            {
                regionAttachment.ComputeWorldVertices(slot, worldVertices, 0, 2);
                region = _atlas.FindRegion(regionAttachment.Name);
                var image = region.page.rendererObject as SKImage;//region.page.texture.getImage() as SKImage;

                var slotColor = SpineColor.Get(slot);
                var regionColor = SpineColor.Get(regionAttachment);
                color.Set(skeletonColor.R * slotColor.R * regionColor.R,
                    skeletonColor.G * slotColor.G * regionColor.G,
                    skeletonColor.B * slotColor.B * regionColor.B,
                    skeletonColor.A * slotColor.A * regionColor.A);

                ctx.Save();
                //ctx.transform(bone.a, bone.c, bone.b, bone.d, bone.worldX, bone.worldY);
                var m = new SKMatrix
                {
                    Values = new[] { bone.a, bone.b, bone.worldX, bone.c, bone.d, bone.worldY, 0, 0, 1 }
                };
                ctx.Concat(ref m);

                ctx.Translate(regionAttachment.offset[0], regionAttachment.offset[1]);
                ctx.RotateRadians((float)(regionAttachment.rotation * Math.PI / 180));
                var atlasScale = regionAttachment.width / region.originalWidth;
                ctx.Scale(atlasScale * regionAttachment.scaleX, atlasScale * regionAttachment.scaleY);

                if (region != null)
                {
                    var w = region.width;
                    var h = region.height;
                    ctx.Translate(w / 2, h / 2);
                    if (region!.degrees == 90)
                    {
                        var t = w;
                        w = h;
                        h = t;
                        ctx.RotateRadians((float)(-Math.PI / 2));
                    }

                    ctx.Scale(1, -1);
                    ctx.Translate(-w / 2, -h / 2);

                    //ctx.GlobalAlpha = color.a;
                    var srcRect = new SKRect(region.x, region.y, region.x + region.originalWidth, region.y + region.originalHeight);
                    var destRect = new SKRect(0, 0, w, h);

                    ctx.DrawImage(image, srcRect, destRect);

                    //ctx.DrawImage(image, region.x, region.y, w, h, 0, 0, w, h);
                    if (debugRendering)
                        ctx.DrawRect(0, 0, w, h, _debugPaint);
                }

                ctx.Restore();
            }
        }
    }

    private void DrawTriangles(Skeleton skeleton, SKCanvas canvas)
    {
        var color = tempColor;
        var skeletonColor = SpineColor.Get(skeleton);
        var drawOrder = skeleton.drawOrder;

        BlendMode? blendMode = null;
        float[] vertices = this.vertices;
        int[] triangles = null;

        for (int i = 0, n = drawOrder.Count; i < n; i++)
        {
            var slot = drawOrder.Items[i];
            var attachment = slot.Attachment;

            SKImage texture = null;
            AtlasRegion region = null;

            if (attachment is RegionAttachment regionAttachment)
            {
                vertices = ComputeRegionVertices(slot, regionAttachment, false);
                triangles = QUAD_TRIANGLES;
                region = regionAttachment.region as AtlasRegion;
                texture = region.page.rendererObject as SKImage;
            }
            else if (attachment is MeshAttachment mesh)
            {
                vertices = ComputeMeshVertices(slot, mesh, false);
                triangles = mesh.triangles;
                region = mesh.region as AtlasRegion;
                texture = region.page.rendererObject as SKImage;
            }
            else
                continue;

            if (texture != null)
            {
                if (slot.data.blendMode != blendMode) blendMode = slot.data.blendMode;

                var slotColor = SpineColor.Get(slot);
                var attachmentColor = SpineColor.Get(attachment);

                color.Set(skeletonColor.R * slotColor.R * attachmentColor.R,
                    skeletonColor.R * slotColor.R * attachmentColor.R,
                    skeletonColor.B * slotColor.B * attachmentColor.B,
                    skeletonColor.A * slotColor.A * attachmentColor.A);

                //ctx.globalAlpha = color.a;

                for (var j = 0; j < triangles.Length; j += 3)
                {
                    var t1 = triangles[j] * 8;
                    var t2 = triangles[j + 1] * 8;
                    var t3 = triangles[j + 2] * 8;

                    var x0 = vertices[t1];
                    var y0 = vertices[t1 + 1];
                    var u0 = vertices[t1 + 6];
                    var v0 = vertices[t1 + 7];
                    var x1 = vertices[t2];
                    var y1 = vertices[t2 + 1];
                    var u1 = vertices[t2 + 6];
                    var v1 = vertices[t2 + 7];
                    var x2 = vertices[t3];
                    var y2 = vertices[t3 + 1];
                    var u2 = vertices[t3 + 6];
                    var v2 = vertices[t3 + 7];

                    DrawTriangle(canvas, texture, x0, y0, u0, v0, x1, y1, u1, v1, x2, y2, u2, v2);

                    if (debugRendering)
                    {
                        var path = new SKPath();
                        path.MoveTo(x0, y0);
                        path.LineTo(x1, y1);
                        path.LineTo(x2, y2);
                        path.Close();
                        canvas.DrawPath(path, _debugPaint);
                    }
                }
            }
        }
        //this.ctx.globalAlpha = 1;
    }

    // Adapted from http://extremelysatisfactorytotalitarianism.com/blog/?p=2120
    // Apache 2 licensed
    private void DrawTriangle(SKCanvas canvas, SKImage img, float x0, float y0, float u0, float v0,
        float x1, float y1, float u1, float v1,
        float x2, float y2, float u2, float v2)
    {
        var ctx = canvas;

        u0 *= img.Width;
        v0 *= img.Height;
        u1 *= img.Width;
        v1 *= img.Height;
        u2 *= img.Width;
        v2 *= img.Height;

        var path = new SKPath();
        path.MoveTo(x0, y0);
        path.LineTo(x1, y1);
        path.LineTo(x2, y2);
        path.Close();

        x1 -= x0;
        y1 -= y0;
        x2 -= x0;
        y2 -= y0;

        u1 -= u0;
        v1 -= v0;
        u2 -= u0;
        v2 -= v0;

        var det = 1 / (u1 * v2 - u2 * v1);

        // linear transformation
        var a = (v2 * x1 - v1 * x2) * det;
        var b = (v2 * y1 - v1 * y2) * det;
        var c = (u1 * x2 - u2 * x1) * det;
        var d = (u1 * y2 - u2 * y1) * det;

        // translation
        var e = x0 - a * u0 - c * v0;
        var f = y0 - b * u0 - d * v0;

        ctx.Save();
        var m = new SKMatrix(a, c, e, b, d, f, 0, 0, 1);
        ctx.Concat(ref m);

        if (m.TryInvert(out var inv))
        {
            path.Transform(inv);
        }

        ctx.ClipPath(path);
        ctx.DrawImage(img, 0, 0);
        //canvas.DrawPath(path, _debugPaint);
        ctx.Restore();
    }

    private float[] ComputeRegionVertices(Slot slot, RegionAttachment region, bool pma)
    {
        var skeletonColor = SpineColor.Get(slot.bone.skeleton);
        var slotColor = SpineColor.Get(slot);
        var regionColor = SpineColor.Get(region);
        var alpha = skeletonColor.A * slotColor.A * regionColor.A;
        var multiplier = pma ? alpha : 1;
        var color = tempColor;
        color.Set(skeletonColor.R * slotColor.R * regionColor.R * multiplier,
            skeletonColor.G * slotColor.G * regionColor.G * multiplier,
            skeletonColor.B * slotColor.B * regionColor.B * multiplier,
            alpha
        );

        region.ComputeWorldVertices(slot, this.vertices, 0, VERTEX_SIZE);

        var vertices = this.vertices;
        var uvs = region.uvs;

        vertices[(int)RegionAttachmentIndex.C1R] = color.R;
        vertices[(int)RegionAttachmentIndex.C1G] = color.G;
        vertices[(int)RegionAttachmentIndex.C1B] = color.B;
        vertices[(int)RegionAttachmentIndex.C1A] = color.A;
        vertices[(int)RegionAttachmentIndex.U1] = uvs[0];
        vertices[(int)RegionAttachmentIndex.V1] = uvs[1];

        vertices[(int)RegionAttachmentIndex.C2R] = color.R;
        vertices[(int)RegionAttachmentIndex.C2G] = color.G;
        vertices[(int)RegionAttachmentIndex.C2B] = color.B;
        vertices[(int)RegionAttachmentIndex.C2A] = color.A;
        vertices[(int)RegionAttachmentIndex.U2] = uvs[2];
        vertices[(int)RegionAttachmentIndex.V2] = uvs[3];

        vertices[(int)RegionAttachmentIndex.C3R] = color.R;
        vertices[(int)RegionAttachmentIndex.C3G] = color.G;
        vertices[(int)RegionAttachmentIndex.C3B] = color.B;
        vertices[(int)RegionAttachmentIndex.C3A] = color.A;
        vertices[(int)RegionAttachmentIndex.U3] = uvs[4];
        vertices[(int)RegionAttachmentIndex.V3] = uvs[5];

        vertices[(int)RegionAttachmentIndex.C4R] = color.R;
        vertices[(int)RegionAttachmentIndex.C4G] = color.G;
        vertices[(int)RegionAttachmentIndex.C4B] = color.B;
        vertices[(int)RegionAttachmentIndex.C4A] = color.A;
        vertices[(int)RegionAttachmentIndex.U4] = uvs[6];
        vertices[(int)RegionAttachmentIndex.V4] = uvs[7];
        return vertices;
    }

    private float[] ComputeMeshVertices(Slot slot, MeshAttachment mesh, bool pma)
    {
        var skeletonColor = SpineColor.Get(slot.bone.skeleton);
        var slotColor = SpineColor.Get(slot);
        var regionColor = SpineColor.Get(mesh);

        var alpha = skeletonColor.A * slotColor.A * regionColor.A;
        var multiplier = pma ? alpha : 1;
        var color = tempColor;
        color.Set(skeletonColor.R * slotColor.R * regionColor.R * multiplier,
            skeletonColor.G * slotColor.G * regionColor.G * multiplier,
            skeletonColor.B * slotColor.B * regionColor.B * multiplier,
            alpha);

        var vertexCount = mesh.worldVerticesLength / 2;
        var vertices = this.vertices;
        if (vertices.Length < mesh.worldVerticesLength)
            this.vertices = vertices = new float[mesh.worldVerticesLength];
        mesh.ComputeWorldVertices(slot, 0, mesh.worldVerticesLength, vertices, 0, VERTEX_SIZE);

        var uvs = mesh.uvs;
        for (int i = 0, u = 0, v = 2; i < vertexCount; i++)
        {
            vertices[v++] = color.R;
            vertices[v++] = color.G;
            vertices[v++] = color.B;
            vertices[v++] = color.A;
            vertices[v++] = uvs[u++];
            vertices[v++] = uvs[u++];
            v += 2;
        }

        return vertices;
    }
}