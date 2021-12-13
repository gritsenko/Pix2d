export default class NodeBase {

    /**
     * 
     * @param {CanvasRenderingContext2D} ctx 
     * @param {ViewPort} vp 
     */
    OnDraw(ctx, vp) {
        this.DrawBBox();
    }
}