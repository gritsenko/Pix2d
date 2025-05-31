import { dotnet } from './_framework/dotnet.js'
import CommonStuff from './CommonStuff.js'

document.addEventListener('DOMContentLoaded', () => {
    CommonStuff.startLoaderProgressbar();
});

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const { setModuleImports } = dotnetRuntime;

setModuleImports("main.js", {
    appStarted: () => CommonStuff.onAppStarted(),
    get: (key) => localStorage.getItem(key),
    set: (key, value) => {
        localStorage.setItem(key, value);
        console.log("Saved: ", key, value);
    },
    remvoe: (key) => localStorage.removeItem(key),
    clear: (key) => localStorage.clear()
});

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
