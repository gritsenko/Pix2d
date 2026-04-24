import { dotnet } from './_framework/dotnet.js'
import CommonStuff from './CommonStuff.js'

document.addEventListener('DOMContentLoaded', () => {
    CommonStuff.startLoaderProgressbar();
});

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);
const isDevelopmentHost = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
const devCacheBust = isDevelopmentHost ? Date.now().toString() : null;

function withCacheBust(uri) {
    if (!devCacheBust) {
        return uri;
    }

    const separator = uri.includes('?') ? '&' : '?';
    return `${uri}${separator}v=${devCacheBust}`;
}

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .withResourceLoader((type, name, defaultUri) => {
        if (!isDevelopmentHost || defaultUri == null) {
            return defaultUri;
        }

        return withCacheBust(defaultUri);
    })
    .create();

const { setModuleImports } = dotnetRuntime;

setModuleImports("main.js", {
    appStarted: () => CommonStuff.onAppStarted(),
    get: (key) => localStorage.getItem(key),
    set: (key, value) => {
        localStorage.setItem(key, value);
        console.log("Saved: ", key, value);
    },
    remove: (key) => localStorage.removeItem(key),
    clear: (key) => localStorage.clear(),
    setTitle: (title) => { document.title = title; },
    openUrl: (url) => { window.open(url, '_blank'); }
});

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [window.location.href]);
