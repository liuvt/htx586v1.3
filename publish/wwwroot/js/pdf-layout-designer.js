(function () {
    const states = new WeakMap();

    function loadScript(src) {
        return new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${src}"]`);
            if (existing) {
                existing.addEventListener('load', resolve, { once: true });
                if (window.pdfjsLib) resolve();
                return;
            }

            const script = document.createElement('script');
            script.src = src;
            script.async = true;
            script.onload = resolve;
            script.onerror = () => reject(new Error(`Không tải được ${src}`));
            document.head.appendChild(script);
        });
    }

    async function ensurePdfJs() {
        if (window.pdfjsLib) return;
        await loadScript('https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.min.js');
        window.pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
    }

    function base64ToBytes(base64) {
        const raw = atob(base64);
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
        return bytes;
    }

    function normalizeField(field) {
        return {
            id: field.id ?? field.Id,
            kind: field.kind ?? field.Kind,
            key: field.key ?? field.Key,
            page: Number(field.page ?? field.Page ?? 1),
            x: Number(field.x ?? field.X ?? 0),
            y: Number(field.y ?? field.Y ?? 0),
            width: Number(field.width ?? field.Width ?? 10),
            height: Number(field.height ?? field.Height ?? 10),
            fontSize: Number(field.fontSize ?? field.FontSize ?? 11),
            minFontSize: Number(field.minFontSize ?? field.MinFontSize ?? 8),
            maxLines: Number(field.maxLines ?? field.MaxLines ?? 1),
            alignment: field.alignment ?? field.Alignment ?? 'Left',
            verticalAlignment: field.verticalAlignment ?? field.VerticalAlignment ?? 'Center',
            bold: Boolean(field.bold ?? field.Bold),
            italic: Boolean(field.italic ?? field.Italic),
            uppercase: Boolean(field.uppercase ?? field.Uppercase),
            clearBackground: Boolean(field.clearBackground ?? field.ClearBackground),
            fit: field.fit ?? field.Fit ?? 'Contain',
            previewText: field.previewText ?? field.PreviewText ?? field.key ?? field.Key ?? ''
        };
    }

    function normalizeOptions(options) {
        options = options || {};
        return {
            showSampleText: options.showSampleText ?? options.ShowSampleText ?? true,
            showFieldBoxes: options.showFieldBoxes ?? options.ShowFieldBoxes ?? true,
            showAllFields: options.showAllFields ?? options.ShowAllFields ?? true,
            showCrosshair: options.showCrosshair ?? options.ShowCrosshair ?? true,
            snapToGrid: options.snapToGrid ?? options.SnapToGrid ?? true,
            gridSize: Math.max(0.01, Number(options.gridSize ?? options.GridSize ?? 0.25)),
            nudgeStep: Math.max(0.01, Number(options.nudgeStep ?? options.NudgeStep ?? 0.25)),
            fontFamily: options.fontFamily ?? options.FontFamily ?? 'Times New Roman'
        };
    }

    function kindCss(field) {
        if (typeof field.kind === 'string') return field.kind.toLowerCase();
        return field.id && field.id.startsWith('image:') ? 'image' : 'text';
    }

    function round(value) {
        return Math.round(value * 100) / 100;
    }

    function snapValue(value, state) {
        if (!state.options.snapToGrid) return round(value);
        const step = state.options.gridSize;
        return round(Math.round(value / step) * step);
    }

    function clampField(field, state) {
        const pageSize = state.pageSizes.get(Number(field.page));
        field.width = Math.max(2, round(field.width));
        field.height = Math.max(2, round(field.height));
        field.x = Math.max(0, round(field.x));
        field.y = Math.max(0, round(field.y));
        if (!pageSize) return;
        field.width = Math.min(field.width, pageSize.width);
        field.height = Math.min(field.height, pageSize.height);
        field.x = Math.min(field.x, Math.max(0, pageSize.width - field.width));
        field.y = Math.min(field.y, Math.max(0, pageSize.height - field.height));
    }

    function updateElementPosition(element, field, state) {
        const scale = state.scale;
        element.style.left = `${field.x * scale}px`;
        element.style.top = `${field.y * scale}px`;
        element.style.width = `${field.width * scale}px`;
        element.style.height = `${field.height * scale}px`;

        const badge = element.querySelector('.pdf-designer-field-coordinates');
        if (badge) badge.textContent = `X ${field.x} · Y ${field.y} · W ${field.width} · H ${field.height}`;
    }

    function fontCss(field, fontSizePoints, state) {
        const family = String(state.options.fontFamily || 'Times New Roman').replace(/"/g, '\\"');
        const style = field.italic ? 'italic' : 'normal';
        const weight = field.bold ? '700' : '400';
        return `${style} ${weight} ${Math.max(1, fontSizePoints) * state.scale}px "${family}", "Times New Roman", serif`;
    }

    function normalizePreviewText(value) {
        return String(value || '')
            .replace(/\r/g, ' ')
            .replace(/\n/g, ' ')
            .split(/\s+/)
            .filter(Boolean)
            .join(' ');
    }

    function measureTextWidth(ctx, text) {
        return ctx.measureText(text || '').width;
    }

    function wrapTextForMeasurement(value, ctx, maxWidthPx) {
        const normalized = normalizePreviewText(value);
        if (!normalized) return [];

        const words = normalized.split(' ').filter(Boolean);
        const lines = [];
        let current = '';

        for (const word of words) {
            const candidate = current ? `${current} ${word}` : word;
            if (measureTextWidth(ctx, candidate) <= maxWidthPx) {
                current = candidate;
                continue;
            }

            if (current) lines.push(current);
            // Giống renderer PDF: giữ nguyên từ dài để vòng lặp giảm font xử lý.
            current = word;
        }

        if (current) lines.push(current);
        return lines;
    }

    function fitSingleLine(value, ctx, maxWidthPx) {
        if (measureTextWidth(ctx, value) <= maxWidthPx) return value;

        const ellipsis = '...';
        let low = 0;
        let high = value.length;
        while (low < high) {
            const middle = Math.floor((low + high + 1) / 2);
            const candidate = value.slice(0, middle).trimEnd() + ellipsis;
            if (measureTextWidth(ctx, candidate) <= maxWidthPx) low = middle;
            else high = middle - 1;
        }

        return low <= 0 ? ellipsis : value.slice(0, low).trimEnd() + ellipsis;
    }

    function wrapText(value, ctx, maxWidthPx, maxLines) {
        const normalized = normalizePreviewText(value);
        if (!normalized) return [];

        if (maxLines <= 1) return [fitSingleLine(normalized, ctx, maxWidthPx)];

        const measured = wrapTextForMeasurement(normalized, ctx, maxWidthPx);
        if (measured.length <= maxLines)
            return measured.map(line => fitSingleLine(line, ctx, maxWidthPx));

        const result = measured.slice(0, maxLines);
        result[result.length - 1] = fitSingleLine(
            measured.slice(maxLines - 1).join(' '),
            ctx,
            maxWidthPx);
        return result;
    }

    function getCanvasFontMetrics(ctx, sample, fontSizePx) {
        const metrics = ctx.measureText(sample || 'Ag');
        const ascent = Number(metrics.actualBoundingBoxAscent || fontSizePx * 0.78);
        const descent = Number(metrics.actualBoundingBoxDescent || fontSizePx * 0.22);
        // Skia: (descent - ascent + leading) * 1.02. Browser không expose leading,
        // nên dùng ascent + descent và hệ số 1.02 để bám sát renderer nhất có thể.
        return {
            ascent,
            descent,
            lineHeight: Math.max(1, (ascent + descent) * 1.02)
        };
    }

    function buildExactTextPreview(field, state) {
        const scale = state.scale;
        const paddingX = 1.0 * scale;
        const paddingY = 0.5 * scale;
        const innerWidth = Math.max(1, field.width * scale - paddingX * 2);
        const innerHeight = Math.max(1, field.height * scale - paddingY * 2);
        const maxLines = Math.max(1, Number(field.maxLines || 1));
        const canvas = state.measureCanvas || (state.measureCanvas = document.createElement('canvas'));
        const ctx = canvas.getContext('2d');
        let fontSize = Math.max(1, Number(field.fontSize || 11));
        const minFontSize = Math.min(fontSize, Math.max(1, Number(field.minFontSize || fontSize)));
        const text = field.uppercase
            ? normalizePreviewText(field.previewText).toLocaleUpperCase('vi-VN')
            : normalizePreviewText(field.previewText);

        let measuredLines = [];
        let metrics = null;
        let widest = 0;
        let blockHeight = 0;

        while (true) {
            ctx.font = fontCss(field, fontSize, state);
            measuredLines = wrapTextForMeasurement(text, ctx, innerWidth);
            metrics = getCanvasFontMetrics(ctx, text, fontSize * scale);
            blockHeight = metrics.lineHeight * measuredLines.length;
            widest = measuredLines.length ? Math.max(...measuredLines.map(line => measureTextWidth(ctx, line))) : 0;

            if ((widest <= innerWidth && blockHeight <= innerHeight && measuredLines.length <= maxLines) || fontSize <= minFontSize)
                break;

            fontSize = Math.max(minFontSize, Math.round((fontSize - 0.25) * 100) / 100);
        }

        ctx.font = fontCss(field, fontSize, state);
        const lines = wrapText(text, ctx, innerWidth, maxLines);
        metrics = getCanvasFontMetrics(ctx, text, fontSize * scale);
        blockHeight = metrics.lineHeight * lines.length;
        widest = lines.length ? Math.max(...lines.map(line => measureTextWidth(ctx, line))) : 0;

        // Renderer tạo PNG có padding rồi căn PNG đó trong field.
        const renderedWidth = Math.max(1, widest + paddingX * 2);
        const renderedHeight = Math.max(1, blockHeight + paddingY * 2);
        let left = 0;
        let top = 0;
        const horizontal = String(field.alignment || 'Left').toLowerCase();
        const vertical = String(field.verticalAlignment || 'Center').toLowerCase();

        if (horizontal === 'center') left = Math.max(0, (field.width * scale - renderedWidth) / 2);
        else if (horizontal === 'right') left = Math.max(0, field.width * scale - renderedWidth);

        if (vertical === 'top') top = 0;
        else if (vertical === 'bottom') top = Math.max(0, field.height * scale - renderedHeight);
        else top = Math.max(0, (field.height * scale - renderedHeight) / 2);

        return {
            text,
            lines,
            fontSize,
            lineHeight: metrics.lineHeight,
            paddingX,
            paddingY,
            renderedWidth,
            renderedHeight,
            left,
            top
        };
    }

    function updateElementAppearance(element, field, state) {
        const options = state.options;
        const selected = field.id === state.selectedId;
        const kind = kindCss(field);
        element.classList.toggle('hide-box', !options.showFieldBoxes && !selected);
        element.classList.toggle('hide-field', !options.showAllFields && !selected);
        element.classList.toggle('show-crosshair', options.showCrosshair && selected);

        const preview = element.querySelector('.pdf-designer-field-preview');
        if (preview) {
            preview.style.display = options.showSampleText ? 'block' : 'none';
            preview.innerHTML = '';
            preview.classList.toggle('pdf-designer-image-placeholder', kind === 'image');

            if (kind === 'image') {
                preview.textContent = field.previewText || `[ẢNH: ${field.key}]`;
                preview.style.left = '0';
                preview.style.top = '0';
                preview.style.width = '100%';
                preview.style.height = '100%';
            } else {
                const rendered = buildExactTextPreview(field, state);
                preview.style.left = `${rendered.left}px`;
                preview.style.top = `${rendered.top}px`;
                preview.style.width = `${rendered.renderedWidth}px`;
                preview.style.height = `${rendered.renderedHeight}px`;
                preview.style.padding = `${rendered.paddingY}px ${rendered.paddingX}px`;
                preview.style.fontFamily = `"${options.fontFamily}", "Times New Roman", serif`;
                preview.style.fontSize = `${rendered.fontSize * state.scale}px`;
                preview.style.fontWeight = field.bold ? '700' : '400';
                preview.style.fontStyle = field.italic ? 'italic' : 'normal';
                preview.style.lineHeight = `${rendered.lineHeight}px`;
                preview.style.textAlign = 'left';
                preview.style.background = field.clearBackground ? 'rgba(255,255,255,.92)' : 'transparent';

                rendered.lines.forEach((line, index) => {
                    const lineElement = document.createElement('div');
                    lineElement.className = 'pdf-designer-preview-line';
                    lineElement.textContent = line;
                    lineElement.style.height = `${rendered.lineHeight}px`;
                    lineElement.style.lineHeight = `${rendered.lineHeight}px`;
                    preview.appendChild(lineElement);
                });

                const badge = element.querySelector('.pdf-designer-field-coordinates');
                if (badge) {
                    badge.textContent = `X ${field.x} · Y ${field.y} · W ${field.width} · H ${field.height} · Font ${rendered.fontSize.toFixed(2).replace(/\\.00$/, '')}`;
                }
            }
        }
    }


    async function renderPages(host, state) {
        host.innerHTML = '';
        state.pageLayers = new Map();
        state.pageSizes = new Map();
        state.fieldElements = new Map();

        for (let pageNumber = 1; pageNumber <= state.pdf.numPages; pageNumber++) {
            const page = await state.pdf.getPage(pageNumber);
            const viewport = page.getViewport({ scale: state.scale });
            const baseViewport = page.getViewport({ scale: 1 });
            state.pageSizes.set(pageNumber, { width: baseViewport.width, height: baseViewport.height });

            const pageShell = document.createElement('div');
            pageShell.className = 'pdf-designer-page-shell';

            const pageLabel = document.createElement('div');
            pageLabel.className = 'pdf-designer-page-label';
            pageLabel.textContent = `Trang ${pageNumber} · ${round(baseViewport.width)} × ${round(baseViewport.height)} pt`;
            pageShell.appendChild(pageLabel);

            const pageWrap = document.createElement('div');
            pageWrap.className = 'pdf-designer-page';
            pageWrap.style.width = `${viewport.width}px`;
            pageWrap.style.height = `${viewport.height}px`;
            pageShell.appendChild(pageWrap);

            const canvas = document.createElement('canvas');
            canvas.width = Math.ceil(viewport.width);
            canvas.height = Math.ceil(viewport.height);
            canvas.style.width = `${viewport.width}px`;
            canvas.style.height = `${viewport.height}px`;
            pageWrap.appendChild(canvas);

            const overlay = document.createElement('div');
            overlay.className = 'pdf-designer-overlay';
            overlay.dataset.page = pageNumber.toString();
            pageWrap.appendChild(overlay);
            state.pageLayers.set(pageNumber, overlay);

            await page.render({
                canvasContext: canvas.getContext('2d'),
                viewport
            }).promise;

            host.appendChild(pageShell);
        }

        renderFieldOverlays(state);
    }

    function makeHandle(direction) {
        const handle = document.createElement('span');
        handle.className = `pdf-designer-field-resize resize-${direction}`;
        handle.dataset.resizeDirection = direction;
        handle.title = `Resize ${direction.toUpperCase()}`;
        return handle;
    }

    function renderFieldOverlays(state) {
        for (const layer of state.pageLayers.values()) layer.innerHTML = '';
        state.fieldElements.clear();

        for (const field of state.fields) {
            const layer = state.pageLayers.get(Number(field.page));
            if (!layer) continue;

            const element = document.createElement('div');
            element.className = `pdf-designer-field pdf-designer-field-${kindCss(field)}`;
            element.dataset.fieldId = field.id;
            element.tabIndex = 0;

            const preview = document.createElement('span');
            preview.className = 'pdf-designer-field-preview';
            element.appendChild(preview);

            const label = document.createElement('span');
            label.className = 'pdf-designer-field-label';
            label.textContent = field.key;
            element.appendChild(label);

            const coordinates = document.createElement('span');
            coordinates.className = 'pdf-designer-field-coordinates';
            element.appendChild(coordinates);

            for (const direction of ['nw', 'ne', 'sw', 'se'])
                element.appendChild(makeHandle(direction));

            clampField(field, state);
            updateElementPosition(element, field, state);
            updateElementAppearance(element, field, state);
            attachFieldEvents(element, field, state);
            layer.appendChild(element);
            state.fieldElements.set(field.id, element);
        }

        if (state.selectedId) selectFieldElement(state, state.selectedId, false);
    }

    function resizeField(field, start, dx, dy, direction, state) {
        let x = start.x;
        let y = start.y;
        let width = start.width;
        let height = start.height;

        if (direction.includes('e')) width = start.width + dx;
        if (direction.includes('s')) height = start.height + dy;
        if (direction.includes('w')) {
            x = start.x + dx;
            width = start.width - dx;
        }
        if (direction.includes('n')) {
            y = start.y + dy;
            height = start.height - dy;
        }

        if (width < 2) {
            if (direction.includes('w')) x -= (2 - width);
            width = 2;
        }
        if (height < 2) {
            if (direction.includes('n')) y -= (2 - height);
            height = 2;
        }

        field.x = snapValue(x, state);
        field.y = snapValue(y, state);
        field.width = snapValue(width, state);
        field.height = snapValue(height, state);
        clampField(field, state);
    }

    function attachFieldEvents(element, field, state) {
        element.addEventListener('mousedown', (event) => {
            if (event.button !== 0) return;
            event.preventDefault();
            event.stopPropagation();

            const resizeDirection = event.target?.dataset?.resizeDirection || null;
            const startClientX = event.clientX;
            const startClientY = event.clientY;
            const start = { x: field.x, y: field.y, width: field.width, height: field.height };

            selectFieldElement(state, field.id, true);
            element.classList.add('is-dragging');

            function onMove(moveEvent) {
                const dx = (moveEvent.clientX - startClientX) / state.scale;
                const dy = (moveEvent.clientY - startClientY) / state.scale;

                if (resizeDirection) {
                    resizeField(field, start, dx, dy, resizeDirection, state);
                } else {
                    field.x = snapValue(start.x + dx, state);
                    field.y = snapValue(start.y + dy, state);
                    clampField(field, state);
                }

                updateElementPosition(element, field, state);
            }

            function onUp() {
                element.classList.remove('is-dragging');
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup', onUp);
                notifyChanged(state, field);
            }

            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
        });

        element.addEventListener('keydown', (event) => {
            if (!['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) return;

            event.preventDefault();
            let step = state.options.nudgeStep;
            if (event.shiftKey) step *= 5;
            if (event.ctrlKey) step /= 5;
            step = Math.max(0.01, step);

            if (event.altKey) {
                if (event.key === 'ArrowLeft') field.width = Math.max(2, snapValue(field.width - step, state));
                if (event.key === 'ArrowRight') field.width = snapValue(field.width + step, state);
                if (event.key === 'ArrowUp') field.height = Math.max(2, snapValue(field.height - step, state));
                if (event.key === 'ArrowDown') field.height = snapValue(field.height + step, state);
            } else {
                if (event.key === 'ArrowLeft') field.x = snapValue(field.x - step, state);
                if (event.key === 'ArrowRight') field.x = snapValue(field.x + step, state);
                if (event.key === 'ArrowUp') field.y = snapValue(field.y - step, state);
                if (event.key === 'ArrowDown') field.y = snapValue(field.y + step, state);
            }

            clampField(field, state);
            updateElementPosition(element, field, state);
            notifyChanged(state, field);
        });
    }

    function selectFieldElement(state, fieldId, notify) {
        state.selectedId = fieldId;
        for (const [id, element] of state.fieldElements) {
            const selected = id === fieldId;
            element.classList.toggle('is-selected', selected);
            const field = state.fields.find(x => x.id === id);
            if (field) updateElementAppearance(element, field, state);
            if (selected) element.focus({ preventScroll: true });
        }

        if (notify && state.dotNetRef) {
            state.dotNetRef.invokeMethodAsync('OnDesignerFieldSelected', fieldId);
        }
    }

    function notifyChanged(state, field) {
        if (!state.dotNetRef) return;
        state.dotNetRef.invokeMethodAsync('OnDesignerFieldChanged', {
            id: field.id,
            kind: kindCss(field),
            key: field.key,
            page: Number(field.page),
            x: field.x,
            y: field.y,
            width: field.width,
            height: field.height
        });
    }

    function applyOptions(state, options) {
        state.options = { ...state.options, ...normalizeOptions(options) };
        for (const field of state.fields) {
            const element = state.fieldElements.get(field.id);
            if (element) updateElementAppearance(element, field, state);
        }
    }

    window.htxPdfLayoutDesigner = {
        init: async function (host, pdfBase64, fields, dotNetRef, zoom, options) {
            await ensurePdfJs();
            this.destroy(host);

            const pdf = await window.pdfjsLib.getDocument({ data: base64ToBytes(pdfBase64) }).promise;
            const state = {
                pdf,
                fields: (fields || []).map(normalizeField),
                dotNetRef,
                scale: zoom || 1.25,
                options: normalizeOptions(options),
                selectedId: null,
                pageLayers: new Map(),
                pageSizes: new Map(),
                fieldElements: new Map()
            };
            states.set(host, state);
            await renderPages(host, state);
        },

        destroy: function (host) {
            const state = states.get(host);
            if (state && state.pdf) {
                try { state.pdf.destroy(); } catch { }
            }
            states.delete(host);
            if (host) host.innerHTML = '';
        },

        selectField: function (host, fieldId) {
            const state = states.get(host);
            if (!state) return;
            selectFieldElement(state, fieldId, false);
            const element = state.fieldElements.get(fieldId);
            if (element) element.scrollIntoView({ block: 'center', inline: 'center', behavior: 'smooth' });
        },

        updateField: function (host, field) {
            const state = states.get(host);
            if (!state) return;

            const updated = normalizeField(field);
            const index = state.fields.findIndex(x => x.id === updated.id);
            if (index >= 0) {
                state.fields[index] = { ...state.fields[index], ...updated };
                clampField(state.fields[index], state);
                const element = state.fieldElements.get(updated.id);
                if (element) {
                    updateElementPosition(element, state.fields[index], state);
                    updateElementAppearance(element, state.fields[index], state);
                }
            }
        },

        setOptions: function (host, options) {
            const state = states.get(host);
            if (!state) return;
            applyOptions(state, options);
        },

        setZoom: async function (host, zoom) {
            const state = states.get(host);
            if (!state) return;
            state.scale = zoom;
            await renderPages(host, state);
        }
    };
})();
