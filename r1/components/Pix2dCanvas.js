const template = document.createElement('template');
template.innerHTML = /*html*/`
  <style>
    :host { }
    ::slotted(*) { }
    .main-viewport{
      width: 100%;
      height: 100%;
    }
  </style>
  <canvas id="mainCanvas" class="main-viewport"></div>
  <slot></slot>
  <slot name="input" ></slot>
`;

class Pix2dCanvasComponent extends HTMLElement {
  constructor() {
    super();
    this._shadowRoot = this.attachShadow({ mode: 'open' });
  }

  static get observedAttributes() {
    return [''];
  }

  connectedCallback() {
    this._shadowRoot.appendChild(template.content.cloneNode(true));

    this.mainCanvas = this._shadowRoot.getElementById("mainCanvas");
    console.log(this.mainCanvas);

    
  }

  attributeChangedCallback(name, oldVal, newVal) {
  }
}
customElements.define('pix2d-canvas', Pix2dCanvasComponent);