(function () {
    "use strict";

    const api = window.signaturePad = window.signaturePad || {};
    api.pads = api.pads || {};

    function nextFrame() {
        return new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    }

    api.initialize = async function (canvasId) {
        // Step 4 is conditionally rendered by Blazor. Wait until layout has a real width.
        await nextFrame();

        const canvas = document.getElementById(canvasId);
        if (!canvas) return false;

        api.destroy(canvasId);

        const context = canvas.getContext("2d");
        if (!context) return false;

        let drawing = false;
        let hasSignature = false;
        let activePointerId = null;
        let lastWidth = 0;
        let lastHeight = 0;

        const applyCanvasSize = () => {
            const rect = canvas.getBoundingClientRect();
            const cssWidth = Math.max(Math.round(rect.width), 1);
            const cssHeight = Math.max(Math.round(rect.height || 220), 1);

            if (cssWidth === lastWidth && cssHeight === lastHeight && canvas.width > 0)
                return true;

            // Only resize before the user starts signing because resizing clears the canvas.
            if (hasSignature) return true;

            const ratio = Math.max(window.devicePixelRatio || 1, 1);
            canvas.width = Math.round(cssWidth * ratio);
            canvas.height = Math.round(cssHeight * ratio);
            canvas.style.width = cssWidth + "px";
            canvas.style.height = cssHeight + "px";

            context.setTransform(ratio, 0, 0, ratio, 0, 0);
            context.lineWidth = 2.2;
            context.lineCap = "round";
            context.lineJoin = "round";
            context.strokeStyle = "#172B28";

            lastWidth = cssWidth;
            lastHeight = cssHeight;
            return rect.width > 0;
        };

        const pointFromClient = (clientX, clientY) => {
            const rect = canvas.getBoundingClientRect();
            return { x: clientX - rect.left, y: clientY - rect.top };
        };

        const beginStroke = (clientX, clientY) => {
            applyCanvasSize();
            drawing = true;
            hasSignature = true;
            const point = pointFromClient(clientX, clientY);
            context.beginPath();
            context.moveTo(point.x, point.y);
            context.lineTo(point.x + 0.01, point.y + 0.01);
            context.stroke();
        };

        const continueStroke = (clientX, clientY) => {
            if (!drawing) return;
            const point = pointFromClient(clientX, clientY);
            context.lineTo(point.x, point.y);
            context.stroke();
        };

        const finishStroke = () => {
            if (!drawing) return;
            drawing = false;
            context.closePath();
            activePointerId = null;
        };

        const prevent = event => {
            if (event.cancelable) event.preventDefault();
            event.stopPropagation();
        };

        const handlers = {};
        const supportsPointer = typeof window.PointerEvent !== "undefined";

        if (supportsPointer) {
            handlers.pointerDown = event => {
                if (event.pointerType === "mouse" && event.button !== 0) return;
                prevent(event);
                activePointerId = event.pointerId;
                try { canvas.setPointerCapture(event.pointerId); } catch (_) { }
                beginStroke(event.clientX, event.clientY);
            };
            handlers.pointerMove = event => {
                if (!drawing || event.pointerId !== activePointerId) return;
                prevent(event);
                continueStroke(event.clientX, event.clientY);
            };
            handlers.pointerEnd = event => {
                if (!drawing || (activePointerId !== null && event.pointerId !== activePointerId)) return;
                prevent(event);
                try { canvas.releasePointerCapture(event.pointerId); } catch (_) { }
                finishStroke();
            };

            canvas.addEventListener("pointerdown", handlers.pointerDown, { passive: false });
            canvas.addEventListener("pointermove", handlers.pointerMove, { passive: false });
            canvas.addEventListener("pointerup", handlers.pointerEnd, { passive: false });
            canvas.addEventListener("pointercancel", handlers.pointerEnd, { passive: false });
            canvas.addEventListener("lostpointercapture", handlers.pointerEnd, { passive: false });
        } else {
            // Fallback for old iOS/WebView where PointerEvent is unavailable.
            handlers.touchStart = event => {
                if (!event.touches || event.touches.length === 0) return;
                prevent(event);
                const touch = event.touches[0];
                beginStroke(touch.clientX, touch.clientY);
            };
            handlers.touchMove = event => {
                if (!drawing || !event.touches || event.touches.length === 0) return;
                prevent(event);
                const touch = event.touches[0];
                continueStroke(touch.clientX, touch.clientY);
            };
            handlers.touchEnd = event => {
                prevent(event);
                finishStroke();
            };
            handlers.mouseDown = event => {
                if (event.button !== 0) return;
                prevent(event);
                beginStroke(event.clientX, event.clientY);
            };
            handlers.mouseMove = event => {
                if (!drawing) return;
                prevent(event);
                continueStroke(event.clientX, event.clientY);
            };
            handlers.mouseUp = event => {
                if (!drawing) return;
                prevent(event);
                finishStroke();
            };

            canvas.addEventListener("touchstart", handlers.touchStart, { passive: false });
            canvas.addEventListener("touchmove", handlers.touchMove, { passive: false });
            canvas.addEventListener("touchend", handlers.touchEnd, { passive: false });
            canvas.addEventListener("touchcancel", handlers.touchEnd, { passive: false });
            canvas.addEventListener("mousedown", handlers.mouseDown, { passive: false });
            window.addEventListener("mousemove", handlers.mouseMove, { passive: false });
            window.addEventListener("mouseup", handlers.mouseUp, { passive: false });
        }

        canvas.style.touchAction = "none";
        canvas.style.msTouchAction = "none";
        canvas.style.userSelect = "none";
        canvas.style.webkitUserSelect = "none";
        canvas.style.webkitTouchCallout = "none";

        applyCanvasSize();

        let resizeObserver = null;
        if (typeof window.ResizeObserver !== "undefined") {
            resizeObserver = new ResizeObserver(() => {
                if (!drawing && !hasSignature) applyCanvasSize();
            });
            resizeObserver.observe(canvas);
        }

        handlers.windowResize = () => {
            if (!drawing && !hasSignature) applyCanvasSize();
        };
        window.addEventListener("resize", handlers.windowResize);
        window.addEventListener("orientationchange", handlers.windowResize);

        api.pads[canvasId] = {
            canvas,
            context,
            handlers,
            supportsPointer,
            resizeObserver,
            hasSignature: () => hasSignature,
            resetSignature: () => { hasSignature = false; },
            resize: applyCanvasSize
        };

        return true;
    };

    api.destroy = function (canvasId) {
        const pad = api.pads[canvasId];
        if (!pad) return;

        const c = pad.canvas;
        const h = pad.handlers;

        if (pad.supportsPointer) {
            c.removeEventListener("pointerdown", h.pointerDown);
            c.removeEventListener("pointermove", h.pointerMove);
            c.removeEventListener("pointerup", h.pointerEnd);
            c.removeEventListener("pointercancel", h.pointerEnd);
            c.removeEventListener("lostpointercapture", h.pointerEnd);
        } else {
            c.removeEventListener("touchstart", h.touchStart);
            c.removeEventListener("touchmove", h.touchMove);
            c.removeEventListener("touchend", h.touchEnd);
            c.removeEventListener("touchcancel", h.touchEnd);
            c.removeEventListener("mousedown", h.mouseDown);
            window.removeEventListener("mousemove", h.mouseMove);
            window.removeEventListener("mouseup", h.mouseUp);
        }

        window.removeEventListener("resize", h.windowResize);
        window.removeEventListener("orientationchange", h.windowResize);
        pad.resizeObserver?.disconnect();
        delete api.pads[canvasId];
    };

    api.clear = function (canvasId) {
        const pad = api.pads[canvasId];
        if (!pad) return false;
        pad.context.clearRect(0, 0, pad.canvas.width, pad.canvas.height);
        pad.resetSignature();
        return true;
    };

    function exportCompactSignature(pad) {
        const source = pad.canvas;
        const sourceContext = pad.context;
        const image = sourceContext.getImageData(0, 0, source.width, source.height);
        const pixels = image.data;

        let minX = source.width;
        let minY = source.height;
        let maxX = -1;
        let maxY = -1;

        // Canvas is transparent; inspect alpha to crop unused space.
        for (let y = 0; y < source.height; y++) {
            for (let x = 0; x < source.width; x++) {
                const alpha = pixels[((y * source.width + x) * 4) + 3];
                if (alpha > 8) {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY) return null;

        const ratio = Math.max(window.devicePixelRatio || 1, 1);
        const padding = Math.max(Math.round(12 * ratio), 8);
        minX = Math.max(0, minX - padding);
        minY = Math.max(0, minY - padding);
        maxX = Math.min(source.width - 1, maxX + padding);
        maxY = Math.min(source.height - 1, maxY + padding);

        const cropWidth = Math.max(1, maxX - minX + 1);
        const cropHeight = Math.max(1, maxY - minY + 1);

        // Blazor Server transports JS interop through SignalR. Export at a compact
        // normalized size so a high-DPI mobile canvas does not exceed the message limit.
        const maxWidth = 720;
        const maxHeight = 260;
        const scale = Math.min(maxWidth / cropWidth, maxHeight / cropHeight, 1);
        const targetWidth = Math.max(1, Math.round(cropWidth * scale));
        const targetHeight = Math.max(1, Math.round(cropHeight * scale));

        const output = document.createElement("canvas");
        output.width = targetWidth;
        output.height = targetHeight;

        const outputContext = output.getContext("2d");
        if (!outputContext) return null;

        // White background keeps JPEG small and compatible with PDF rendering.
        outputContext.fillStyle = "#ffffff";
        outputContext.fillRect(0, 0, targetWidth, targetHeight);
        outputContext.drawImage(
            source,
            minX, minY, cropWidth, cropHeight,
            0, 0, targetWidth, targetHeight);

        return output.toDataURL("image/jpeg", 0.82);
    }

    api.getData = function (canvasId) {
        const pad = api.pads[canvasId];
        if (!pad || !pad.hasSignature()) return null;
        return exportCompactSignature(pad);
    };
})();
