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
    let pendingFrameTime = 0;
    let lastError = "";
    let initialTickComplete = false;
    let runtimeStage = "browser startup";
    let fullscreenPending = false;
    let resizeObserver = null;
    let immediateAudioParameterDepth = 0;
    let immediateAudioParameterWrites = 0;
    const targetFrameTime = 1000 / 60;
    const maximumCanvasWidth = 2560;
    const maximumCanvasHeight = 1440;
    const maximumCanvasPixels = maximumCanvasWidth * maximumCanvasHeight;
    const storagePrefix = "minimal-bastion:file:";
    const pointer = {
        x: 0,
        y: 0,
        leftPressed: false,
        leftReleased: false,
        leftDown: false,
        rightPressed: false,
        middlePressed: false
    };

    const setTargetAtTime = window.nkAudioParam?.SetTargetAtTime;
    if (typeof setTargetAtTime === "function") {
        window.nkAudioParam.SetTargetAtTime = function (uid, module, data) {
            if (immediateAudioParameterDepth <= 0)
                return setTargetAtTime(uid, module, data);

            const parameter = window.nkJSObject?.GetObject(uid);
            if (!parameter)
                return setTargetAtTime(uid, module, data);

            const target = module.HEAPF32[(data + 0) >> 2];
            const startTime = module.HEAPF32[(data + 4) >> 2];
            if (!Number.isFinite(target) || startTime !== 0)
                return setTargetAtTime(uid, module, data);

            parameter.cancelScheduledValues(0);
            parameter.value = target;
            immediateAudioParameterWrites++;
        };
    }

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
        const cssWidth = Math.max(1, holder.clientWidth);
        const cssHeight = Math.max(1, holder.clientHeight);
        const desiredPixelRatio = Math.min(2, Math.max(1, window.devicePixelRatio || 1));
        const pixelRatio = Math.min(
            desiredPixelRatio,
            maximumCanvasWidth / cssWidth,
            maximumCanvasHeight / cssHeight,
            Math.sqrt(maximumCanvasPixels / (cssWidth * cssHeight)));
        const width = Math.max(1, Math.round(cssWidth * pixelRatio));
        const height = Math.max(1, Math.round(cssHeight * pixelRatio));
        const changed = canvas.width !== width || canvas.height !== height;
        if (canvas.width !== width) canvas.width = width;
        if (canvas.height !== height) canvas.height = height;
        if (changed) publishBrowserDisplayState();
    }

    function scheduleCanvasResize() {
        resizeCanvas();
        window.requestAnimationFrame(() => {
            resizeCanvas();
            window.requestAnimationFrame(resizeCanvas);
        });
        window.setTimeout(resizeCanvas, 100);
        window.setTimeout(resizeCanvas, 300);
    }

    function isGameFullscreen() {
        const holder = document.getElementById("canvasHolder");
        return !!holder && document.fullscreenElement === holder;
    }

    function publishBrowserDisplayState() {
        const canvas = document.getElementById("theCanvas");
        instance?.invokeMethodAsync(
            "SetBrowserDisplayState",
            isGameFullscreen(),
            fullscreenPending,
            canvas?.width || 0,
            canvas?.height || 0)
            .catch(error => console.warn("Browser display state could not be synchronized.", error));
    }

    async function setFullscreen(enabled) {
        const holder = document.getElementById("canvasHolder");
        if (!holder) return;
        const desired = !!enabled;
        if (desired === isGameFullscreen()) {
            fullscreenPending = false;
            scheduleCanvasResize();
            return;
        }

        fullscreenPending = true;
        publishBrowserDisplayState();
        try {
            if (desired)
                await holder.requestFullscreen();
            else if (document.fullscreenElement)
                await document.exitFullscreen();
        } catch (error) {
            console.warn("Fullscreen display change was not accepted.", error);
        } finally {
            fullscreenPending = false;
            publishBrowserDisplayState();
            scheduleCanvasResize();
        }
    }

    function stopWithError(error, summary) {
        running = false;
        lastError = error?.stack || error?.message || String(error);
        console.error(summary, error);
        publishDiagnostics();
        const errorUi = document.getElementById("blazor-error-ui");
        const errorSummary = document.getElementById("browser-error-summary");
        const detail = error?.message || String(error);
        if (errorSummary)
            errorSummary.textContent = `${summary} (${runtimeStage}) ${detail}`;
        errorUi?.classList.add("visible");
        window.minimalBastionLoading?.fail();
    }

    function frame(timestamp) {
        if (!running || !instance) return;
        if (previousFrameAt > 0) {
            const frameGap = timestamp - previousFrameAt;
            maximumFrameGap = Math.max(maximumFrameGap, frameGap);
            pendingFrameTime = Math.min(targetFrameTime * 2, pendingFrameTime + frameGap);
        } else {
            pendingFrameTime = targetFrameTime;
        }
        previousFrameAt = timestamp;

        if (pendingFrameTime + 0.25 < targetFrameTime) {
            window.requestAnimationFrame(frame);
            return;
        }
        pendingFrameTime = Math.max(0, pendingFrameTime - targetFrameTime);

        const tickStartedAt = performance.now();
        try {
            if (!initialTickComplete) {
                runtimeStage = "game initialization";
                window.minimalBastionLoading?.update("INITIALIZING GRAPHICS AND GAME DATA");
            }
            instance.invokeMethod("Tick");
            if (!initialTickComplete) {
                initialTickComplete = true;
                runtimeStage = "main menu";
                window.minimalBastionLoading?.complete();
            }
        } catch (error) {
            stopWithError(error, "The browser build stopped during gameplay.");
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
            canvasPixelRatio: (document.getElementById("theCanvas")?.width || 0) /
                Math.max(1, document.getElementById("theCanvas")?.clientWidth || 1),
            devicePixelRatio: window.devicePixelRatio || 1,
            runtimeStage,
            immediateAudioParameterWrites,
            lastError
        };
    }

    function publishDiagnostics() {
        const canvas = document.getElementById("theCanvas");
        if (canvas)
            canvas.setAttribute("data-performance", JSON.stringify(getDiagnostics()));
    }

    function preventBrowserShortcuts(event) {
        if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", " ", "Tab", "F11"].includes(event.key))
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
        audio: {
            setImmediateParameters(enabled) {
                if (enabled)
                    immediateAudioParameterDepth++;
                else
                    immediateAudioParameterDepth = Math.max(0, immediateAudioParameterDepth - 1);
            }
        },
        pointer: {
            read() {
                const snapshot = {
                    x: pointer.x,
                    y: pointer.y,
                    leftPressed: pointer.leftPressed,
                    leftReleased: pointer.leftReleased,
                    leftDown: pointer.leftDown,
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
        setFullscreen,
        setRuntimeStage(stage) {
            runtimeStage = stage || "gameplay";
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
                    pendingFrameTime = 0;
                    lastError = "";
                }
                publishDiagnostics();
                return result;
            }
        },
        start(dotNetInstance) {
            instance = dotNetInstance;
            publishBrowserDisplayState();
            scheduleCanvasResize();
            window.addEventListener("resize", scheduleCanvasResize);
            window.visualViewport?.addEventListener("resize", scheduleCanvasResize);
            document.addEventListener("fullscreenchange", () => {
                fullscreenPending = false;
                publishBrowserDisplayState();
                scheduleCanvasResize();
            });
            document.addEventListener("fullscreenerror", () => {
                fullscreenPending = false;
                publishBrowserDisplayState();
                scheduleCanvasResize();
            });
            const holder = document.getElementById("canvasHolder");
            if (holder && window.ResizeObserver) {
                resizeObserver?.disconnect();
                resizeObserver = new ResizeObserver(scheduleCanvasResize);
                resizeObserver.observe(holder);
            }
            window.addEventListener("keydown", preventBrowserShortcuts, { passive: false });
            window.addEventListener("paste", event => {
                clipboardText = event.clipboardData?.getData("text") || clipboardText;
            });
            window.addEventListener("wheel", event => event.preventDefault(), { passive: false });
            document.getElementById("theCanvas")?.addEventListener("contextmenu", event => event.preventDefault());
            const canvas = document.getElementById("theCanvas");
            canvas?.addEventListener("webglcontextlost", event => {
                event.preventDefault();
                stopWithError(new Error("WebGL graphics context lost"), "Browser graphics resources were interrupted.");
            });
            canvas?.addEventListener("pointermove", updatePointerPosition);
            canvas?.addEventListener("pointerdown", event => {
                updatePointerPosition(event);
                if (event.button === 0) {
                    pointer.leftPressed = true;
                    pointer.leftDown = true;
                }
                else if (event.button === 1) pointer.middlePressed = true;
                else if (event.button === 2) pointer.rightPressed = true;
                canvas.setPointerCapture?.(event.pointerId);
            });
            canvas?.addEventListener("pointerup", event => {
                updatePointerPosition(event);
                if (event.button === 0) {
                    pointer.leftReleased = true;
                    pointer.leftDown = false;
                }
                canvas.releasePointerCapture?.(event.pointerId);
            });
            canvas?.addEventListener("pointercancel", () => {
                pointer.leftDown = false;
                pointer.leftReleased = true;
            });
            running = true;
            initialTickComplete = false;
            runtimeStage = "first game frame";
            window.minimalBastionLoading?.update("STARTING GRAPHICS PIPELINE");
            sampleStartedAt = performance.now();
            previousFrameAt = 0;
            pendingFrameTime = 0;
            publishDiagnostics();
            window.requestAnimationFrame(frame);
        }
    };
})();
