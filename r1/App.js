import { Pix2dApp } from "./core/Pix2dApp.js";

export class App {

    static instace = new App();

    constructor(){
        console.log("Pix2d loading...")

        const cnavasContainer = document.getElementById("canvas-container");
        this.Pix2dApp = new Pix2dApp(cnavasContainer);
    }
}

console.log(App.instace);