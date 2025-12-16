window.gameLoop = {
    start: (dotNetHelper) => {
        const loop = () => {
            // console.log("JS Tick");
            dotNetHelper.invokeMethodAsync('GameLoop');
            requestAnimationFrame(loop);
        };
        requestAnimationFrame(loop);
    },
    playSound: (elementId) => {
        const audio = document.getElementById(elementId);
        if (audio) {
            audio.currentTime = 0;
            audio.play();
        }
    },
    saveHighScore: (score) => {
        localStorage.setItem('stackTower_highScore', score);
    },
    getHighScore: () => {
        return parseInt(localStorage.getItem('stackTower_highScore')) || 0;
    }
};
