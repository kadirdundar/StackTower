window.gameLoop = {
    start: (dotNetHelper) => {
        const loop = () => {
            // console.log("JS Tick");
            dotNetHelper.invokeMethodAsync('GameLoop');
            requestAnimationFrame(loop);
        };
        requestAnimationFrame(loop);
    }
};
