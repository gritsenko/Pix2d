import { Pix2dApp } from "./core/Pix2dApp.js";
import * as PIXI from './core/vendor/pixi.min.mjs';

export class App {

    static instace = new App();

    constructor() {
        console.log("Pix2d loading...")

        const cnavasContainer = document.getElementById("canvas-container");
        //this.Pix2dApp = new Pix2dApp(cnavasContainer);
        const app = new PIXI.Application();

        cnavasContainer.appendChild(app.view);

        // load the texture we need
        app.loader.add('bunny', 'Assets/Store_Logo.png').load((loader, resources) => {
            // This creates a texture from a 'bunny.png' image
            const bunny = new PIXI.Sprite(resources.bunny.texture);

            // Setup the position of the bunny
            bunny.x = app.renderer.width / 2;
            bunny.y = app.renderer.height / 2;

            // Rotate around the center
            bunny.anchor.x = 0.5;
            bunny.anchor.y = 0.5;

            // Add the bunny to the scene we are building
            app.stage.addChild(bunny);

            // Listen for frame updates
            // app.ticker.add(() => {
            //     // each frame we spin the bunny around a bit
            //     bunny.rotation += 0.01;
            // });
        });
    }
}

console.log(App.instace);