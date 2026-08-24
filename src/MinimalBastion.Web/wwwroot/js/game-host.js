window.minimalBastion = (() => {
    let instance = null;
    let running = false;
    let clipboardText = "";
    let frameCount = 0;
    let sampleStartedAt = 0;
    let accumulatedTickTime = 0;
    let maximumTickTime = 0;
    let maximumFrameGap = 0;
    let previousFrameAt = 0;
    let lastError = "";
    const storagePrefix = "minimal-bastion:file:";
    const pointer = {
        x: 0,
        y: 0,
        leftPressed: false,
        leftReleased: false,
        rightPressed: false,
        middlePressed: false
    };

    function updatePointerPosition(event) {
        const canvas = document.getElementById("theCanvas");
        if (!canvas) return;
        const bounds = canvas.getBoundingClientRect();
        pointer.x = Math.round((event.clientX - bounds.left) * canvas.width / Math.max(1, bounds.width));
        pointer.y = Math.round((event.clientY - bounds.top) * canvas.height / Math.max(1, bounds.height));
    }

    function resizeCanvas() {
        const canvas = document.getElementById("theCanvas");
        const holder = document.getElementById("canvasHolder");
        if (!canvas || !holder) return;
        const pixelRatio = Math.min(2, Math.max(1, window.devicePixelRatio || 1));
        canvas.width = Math.max(1, Math.round(holder.clientWidth * pixelRatio));
        canvas.height = Math.max(1, Math.round(holder.clientHeight * pixelRatio));
        canvas.style.width = `${holder.clientWidth}px`;
        canvas.style.height = `${holder.clientHeight}px`;
    }

    function frame(timestamp) {
        if (!running || !instance) return;
        if (previousFrameAt > 0)
            maximumFrameGap = Math.max(maximumFrameGap, timestamp - previousFrameAt);
        previousFrameAt = timestamp;

        const tickStartedAt = performance.now();
        try {
            instance.invokeMethod("Tick");
        } catch (error) {
            running = false;
            lastError = error?.stack || error?.message || String(error);
            console.error("Minimal Bastion stopped after an unrecoverable game-loop error.", error);
            publishDiagnostics();
            document.getElementById("blazor-error-ui")?.classList.add("visible");
            return;
        }

        const tickTime = performance.now() - tickStartedAt;
        frameCount++;
        accumulatedTickTime += tickTime;
        maximumTickTime = Math.max(maximumTickTime, tickTime);
        if (frameCount % 30 === 0)
            publishDiagnostics();
        window.requestAnimationFrame(frame);
    }

    function getDiagnostics() {
        const now = performance.now();
        const elapsed = Math.max(1, now - sampleStartedAt);
        return {
            frames: frameCount,
            elapsedMs: elapsed,
            framesPerSecond: frameCount * 1000 / elapsed,
            averageTickMs: frameCount > 0 ? accumulatedTickTime / frameCount : 0,
            maximumTickMs: maximumTickTime,
            maximumFrameGapMs: maximumFrameGap,
            canvasWidth: document.getElementById("theCanvas")?.width || 0,
            canvasHeight: document.getElementById("theCanvas")?.height || 0,
            devicePixelRatio: window.devicePixelRatio || 1,
            lastError
        };
    }

    function publishDiagnostics() {
        const canvas = document.getElementById("theCanvas");
        if (canvas)
            canvas.setAttribute("data-performance", JSON.stringify(getDiagnostics()));
    }

    function preventBrowserShortcuts(event) {
        if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", " ", "Tab"].includes(event.key))
            event.preventDefault();
    }

    return {
        storage: {
            readAll() {
                const files = {};
                for (let index = 0; index < localStorage.length; index++) {
                    const key = localStorage.key(index);
                    if (!key || !key.startsWith(storagePrefix)) continue;
                    files[key.substring(storagePrefix.length)] = localStorage.getItem(key) || "";
                }
                return files;
            },
            write(path, contents) {
                localStorage.setItem(storagePrefix + path, contents);
            },
            remove(path) {
                localStorage.removeItem(storagePrefix + path);
            }
        },
        clipboard: {
            read() {
                return clipboardText;
            },
            write(text) {
                clipboardText = text || "";
                navigator.clipboard?.writeText(clipboardText).catch(() => {});
                return true;
            }
        },
        pointer: {
            read() {
                const snapshot = {
                    x: pointer.x,
                    y: pointer.y,
                    leftPressed: pointer.leftPressed,
                    leftReleased: pointer.leftReleased,
                    rightPressed: pointer.rightPressed,
                    middlePressed: pointer.middlePressed,
                    active: !document.hidden
                };
                pointer.leftPressed = false;
                pointer.leftReleased = false;
                pointer.rightPressed = false;
                pointer.middlePressed = false;
                return snapshot;
            }
        },
        setFullscreen(enabled) {
            if (enabled && !document.fullscreenElement)
                document.getElementById("canvasHolder")?.requestFullscreen().catch(() => {});
            else if (!enabled && document.fullscreenElement)
                document.exitFullscreen().catch(() => {});
        },
        hasInputFocus() {
            return !document.hidden && document.hasFocus();
        },
        diagnostics: {
            read(reset = false) {
                const now = performance.now();
                const result = getDiagnostics();
                if (reset) {
                    frameCount = 0;
                    sampleStartedAt = now;
                    accumulatedTickTime = 0;
                    maximumTickTime = 0;
                    maximumFrameGap = 0;
                    previousFrameAt = 0;
                    lastError = "";
                }
                publishDiagnostics();
                return result;
            }
        },
        start(dotNetInstance) {
            instance = dotNetInstance;
            resizeCanvas();
            window.addEventListener("resize", resizeCanvas);
            document.addEventListener("fullscreenchange", () => window.requestAnimationFrame(resizeCanvas));
            window.addEventListener("keydown", preventBrowserShortcuts, { passive: false });
            window.addEventListener("paste", event => {
                clipboardText = event.clipboardData?.getData("text") || clipboardText;
            });
            window.addEventListener("wheel", event => event.preventDefault(), { passive: false });
            document.getElementById("theCanvas")?.addEventListener("contextmenu", event => event.preventDefault());
            const canvas = document.getElementById("theCanvas");
            canvas?.addEventListener("pointermove", updatePointerPosition);
            canvas?.addEventListener("pointerdown", event => {
                updatePointerPosition(event);
                if (event.button === 0) pointer.leftPressed = true;
                else if (event.button === 1) pointer.middlePressed = true;
                else if (event.button === 2) pointer.rightPressed = true;
                canvas.setPointerCapture?.(event.pointerId);
            });
            canvas?.addEventListener("pointerup", event => {
                updatePointerPosition(event);
                if (event.button === 0) pointer.leftReleased = true;
                canvas.releasePointerCapture?.(event.pointerId);
            });
            running = true;
            sampleStartedAt = performance.now();
            publishDiagnostics();
            window.requestAnimationFrame(frame);
        }
    };
})();
