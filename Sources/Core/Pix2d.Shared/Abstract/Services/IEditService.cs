using Pix2d.Abstract.Edit;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.Abstract
{
    public interface IEditService
    {
        SKNode CurrentEditedNode { get; }
        SKNode FrameEditorNode { get; }

        void ShowNodeEditor();

        void HideNodeEditor();

        void RequestEdit(SKNode[] nodes);
        void ApplyCurrentEdit();
        void GroupNodes(SKNode[] nodes);
        void UngroupNodes(GroupNode group);

        void Resize(IContainerNode containerNode, SKSize size);
        void CropCurrentSprite(SKSize size, float horizontalAnchor, float verticalAnchor);
        void CropCurrentSprite(SKRect newBounds);

        INodeEditor GetCurrentEditor();
        void AddEffect(ISKNodeEffect effect);
        void RemoveEffect(ISKNodeEffect effect);
        void BakeEffect(ISKNodeEffect effect);
    }
}