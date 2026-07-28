(function () {
    'use strict';

    function init(options) {
        const editor = options.editor;
        const editorNode = editor?.getDomNode();
        if (!editor || !editorNode || !options.uploadUrl) return;

        const queue = [];
        let activeUploads = 0;
        const concurrency = options.concurrency || 3;
        const retries = options.retries ?? 5;

        function reportError(error) {
            console.error('Markdown image upload failed.', error);
            if (options.onError) options.onError(error);
        }

        function replacePlaceholder(placeholder, replacement) {
            const model = editor.getModel();
            if (model) model.setValue(model.getValue().replace(placeholder, replacement));
        }

        async function upload(slot, attempt) {
            const form = new FormData();
            form.append('file', slot.file, slot.fileName);
            try {
                const response = await fetch(options.uploadUrl, { method: 'POST', body: form });
                if (response.status === 429 && attempt < retries) {
                    const retryAfter = Number.parseInt(response.headers.get('Retry-After') || '0', 10);
                    await new Promise(resolve => setTimeout(resolve, retryAfter > 0 ? retryAfter * 1000 : 60000));
                    return upload(slot, attempt + 1);
                }
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                const result = await response.json();
                replacePlaceholder(slot.placeholder, `![](${result.InternetPath})`);
            } catch (error) {
                replacePlaceholder(slot.placeholder, '');
                reportError(error);
            }
        }

        function pump() {
            while (activeUploads < concurrency && queue.length) {
                const slot = queue.shift();
                activeUploads++;
                upload(slot, 0).finally(() => {
                    activeUploads--;
                    pump();
                });
            }
        }

        function enqueue(files) {
            if (!files.length) return;
            const slots = files.map(file => {
                const mimeExtension = (file.type.split('/')[1] || 'png').replace('jpeg', 'jpg').split('+')[0];
                const originalExtension = file.name.includes('.') ? file.name.split('.').pop() : '';
                const extension = originalExtension || mimeExtension;
                const fileName = `paste-${crypto.randomUUID().replaceAll('-', '')}.${extension}`;
                return {
                    file,
                    fileName,
                    placeholder: `![uploading...](${fileName})`
                };
            });

            const selection = editor.getSelection();
            const model = editor.getModel();
            const line = model.getLineContent(selection.startLineNumber);
            const prefix = line.trim() && selection.startColumn > 1 ? '\n' : '';
            editor.executeEdits('markdown-image-upload', [{
                range: selection,
                text: prefix + slots.map(slot => slot.placeholder).join('\n') + '\n',
                forceMoveMarkers: true
            }]);
            queue.push(...slots);
            pump();
        }

        document.addEventListener('paste', event => {
            if (!editor.hasTextFocus()) return;
            const files = Array.from(event.clipboardData?.items || [])
                .filter(item => item.kind === 'file' && item.type.startsWith('image/'))
                .map(item => item.getAsFile())
                .filter(Boolean);
            if (!files.length) return;
            event.preventDefault();
            event.stopPropagation();
            enqueue(files);
        }, true);

        editorNode.addEventListener('dragover', event => {
            const hasImage = Array.from(event.dataTransfer?.items || [])
                .some(item => item.kind === 'file' && item.type.startsWith('image/'));
            if (hasImage) {
                event.preventDefault();
                event.stopPropagation();
            }
        }, true);

        editorNode.addEventListener('drop', event => {
            const files = Array.from(event.dataTransfer?.files || []).filter(file => file.type.startsWith('image/'));
            if (!files.length) return;
            event.preventDefault();
            event.stopPropagation();
            editor.focus();
            enqueue(files);
        }, true);
    }

    window.AiursoftMarkdownImageUpload = { init };
})();
