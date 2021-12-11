const template = document.createElement('template');
template.innerHTML = /*html*/`
  <style>
    :host { }
    ::slotted(*) { }
    .highlight {
      color: blue;
    }
  </style>
  <div class="highlight">Mata fata</div>
  <div>
    Top bar will be here
  </div>
  <slot></slot>
  <slot name="input" ></slot>
`;

class MainViewComponent extends HTMLElement {
  constructor() {
    super();
    this._shadowRoot = this.attachShadow({ mode: 'open' });
  }

  static get observedAttributes() {
    return [''];
  }

  connectedCallback() {
    this._shadowRoot.appendChild(template.content.cloneNode(true));
  }

  attributeChangedCallback(name, oldVal, newVal) {
  }
}
customElements.define('main-view', MainViewComponent);