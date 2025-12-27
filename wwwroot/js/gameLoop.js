window.gameLoop = {
    start: (dotNetHelper) => {
        let lastTime = performance.now();

        const loop = (currentTime) => {
            const deltaTime = (currentTime - lastTime) / 1000; // Convert to seconds
            lastTime = currentTime;

            // Cap deltaTime to avoid massive jumps (e.g. if tab was inactive)
            const cappedDeltaTime = Math.min(deltaTime, 0.1);

            dotNetHelper.invokeMethodAsync('GameLoop', cappedDeltaTime);
            requestAnimationFrame(loop);
        };

        requestAnimationFrame(loop);

        // Direct input capture for low latency
        const container = document.getElementById('game-container');
        if (container) {
            container.addEventListener('pointerdown', (e) => {
                // Prevent duplicate handling if we ever add back @onclick
                // e.stopPropagation(); 
                dotNetHelper.invokeMethodAsync('HandleJSInput');
            });
        }
    },
    playSound: (elementId) => {
        const audio = document.getElementById(elementId);
        if (audio) {
            audio.currentTime = 0;
            audio.play().catch(e => console.error("Sound play failed:", e));
        }
    },
    saveHighScore: (score) => {
        localStorage.setItem('stackTower_highScore', score);
    },
    getHighScore: () => {
        return parseInt(localStorage.getItem('stackTower_highScore')) || 0;
    },
    getContainerHeight: () => {
        return window.innerHeight;
    }
};
