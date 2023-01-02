import { BrotliDecode } from './decode.min.js';

//const progressText = document.getElementById("progress-text");

Blazor.start({
    loadBootResource: function (type, name, defaultUri, integrity) {
        if (type !== 'dotnetjs' && location.hostname !== 'localhost' && location.hostname !== 'my.gritsenko.biz') {
            return (async function () {
                const response = await fetch(defaultUri + '.br', { cache: 'no-cache' });
                if (!response.ok) {
                    throw new Error(response.statusText);
                }
                const originalResponseBuffer = await response.arrayBuffer();
                const originalResponseArray = new Int8Array(originalResponseBuffer);
                const decompressedResponseArray = BrotliDecode(originalResponseArray);
                const contentType = type ===
                    'dotnetwasm' ? 'application/wasm' : 'application/octet-stream';
                return new Response(decompressedResponseArray,
                    { headers: { 'content-type': contentType } });
            })();
        }
    }
}).then(function () {
    console.log("App Loaded");
});

let progress = 0;
const progressBar = document.getElementById("progress-value");

function updateProgress() {
    progress = Math.ceil(progress + (100 - progress) / 10);

    if (progressBar !== null)
        progressBar.style.width = progress + "%";
}

const interval = setInterval(updateProgress, 100);

window.hideSplash = () => {

    clearInterval(interval);

    const splash = document.getElementById("splash");
    if (splash !== undefined && splash !== null)
        splash.parentNode.removeChild(splash);
}