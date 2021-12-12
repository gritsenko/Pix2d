export class Pix2dApp {

    /** HTMLElement */
    canvasContainer = {};

    pointerPos = { x: 0, y: 0 };

    constructor(canvasContainer) {
        console.log(canvasContainer);
        this.canvasContainer = canvasContainer;

        this.createCanvas(this.canvasContainer);
    }

    setPosition(e) {
        let rect = e.target.getBoundingClientRect();
        let x = e.clientX - rect.left; //x position within the element.
        let y = e.clientY - rect.top; //y position within the element.
        this.pointerPos.x = x;
        this.pointerPos.y = y;
    }

    /**
     * @param {HTMLElement} container 
     */
    createCanvas(container) {
        this.canvas = document.createElement("canvas");
        container.appendChild(this.canvas);

        window.addEventListener('resize', (e) => this.resize(e));
        this.canvas.addEventListener('mousemove', (e) => this.draw(e));
        this.canvas.addEventListener('mousedown', (e) => this.setPosition(e));
        this.canvas.addEventListener('mouseenter', (e) => this.setPosition(e));

        const ctx = this.canvas.getContext('2d');
        this.ctx = ctx;
        this.resize();

        ctx.fillStyle = "white";
        ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
    }

    resize() {
        const bounds = this.canvasContainer.getBoundingClientRect();
        // Get the device pixel ratio, falling back to 1.
        const dpr = window.devicePixelRatio || 1;
        console.log(`Scale factor ${dpr}`);
        // Give the canvas pixel dimensions of their CSS
        // size * the device pixel ratio.
        const canvas = this.ctx.canvas;

        // Set up CSS size.

        const w = Math.ceil(bounds.width);
        const h = Math.ceil(bounds.height);

        canvas.style.width = w + 'px';
        canvas.style.height = h + 'px';

        canvas.width = w * dpr;
        canvas.height =  h * dpr;
        // Scale all drawing operations by the dpr, so you
        // don't have to worry about the difference.
        this.ctx.scale(dpr, dpr);
    }

    draw(e) {
        const ctx = this.ctx;
        const pos = this.pointerPos;
        // mouse left button must be pressed
        if (e.buttons !== 1) return;

        ctx.beginPath(); // begin

        ctx.imageSmoothingQuality = "high";

        const dpr = 1/window.devicePixelRatio || 1;
        ctx.lineWidth = dpr;
        ctx.lineCap = 'round';
        ctx.strokeStyle = '#c0392b';

        ctx.moveTo(pos.x, pos.y); // from
        this.setPosition(e);
        ctx.lineTo(pos.x, pos.y); // to

        ctx.stroke(); // draw it!
    }

}

