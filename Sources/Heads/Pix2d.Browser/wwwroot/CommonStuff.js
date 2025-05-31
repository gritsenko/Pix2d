export default class CommonStuff {

    static startLoaderProgressbar() {

        let progress = 0;
        let maxProgress = 100;
        const timer = setInterval(() => {
            const d = (maxProgress - progress) / 30;
            progress = Math.ceil(progress + d);

            const progressBar = document.getElementById("progress-value");
            //console.log(progressBar);
            if (progressBar) {
                progressBar.style.width = `${progress}%`;
            }
            //console.log(progress);

            if (timer !== null && (progress >= maxProgress || !progressBar)) {
                clearInterval(timer);
            }
        }, 10);
    }

    static onAppStarted() {
        console.log("App loaded, removing loader stuff");
        setTimeout(() => {
            const splash = document.getElementById("avalonia-splash");
            console.log(splash);
            splash.remove();
        }, 500);
    }

}
