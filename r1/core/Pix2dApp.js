import Pix2dCanvas from "./Pix2dCanvas.js";

export class Pix2dApp {

    constructor(canvasContainer) {
        console.log(canvasContainer);
        this.canvas = new Pix2dCanvas(canvasContainer);
    }
}

