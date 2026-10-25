window.MathJax = {
    tex: { inlineMath: [['$', '$'], ['\\(', '\\)']], displayMath: [['$$', '$$'], ['\\[', '\\]']] },
    svg: { fontCache: 'global' }
};

// Configure the existing Monaco AMD loader before it is loaded.
if (document.getElementById('server-notes-editor')) {
    window.require = { paths: { vs: '/node_modules/monaco-editor/min/vs' } };
}

document.addEventListener('DOMContentLoaded', async function () {
    const root = document.getElementById('server-notes');
    if (!root) return;
    const textarea = document.getElementById('NotesMarkdown');
    const preview = document.getElementById('server-notes-preview');
    const ui = window.AiursoftMarkdownUi;
    if (!ui) return; // Keep the ordinary textarea usable if assets cannot load.
    try {
        if (document.getElementById('server-notes-editor')) {
            await ui.createMarkdownEditor({
                editorContainer: document.getElementById('server-notes-editor'),
                textarea: textarea,
                previewContainer: preview,
                editorPane: document.getElementById('server-notes-editor-pane'),
                previewPane: document.getElementById('server-notes-preview-pane'),
                form: document.getElementById('server-form'),
                loadMonaco: function () { return ui.loadMonacoFromAmd(); },
                theme: root.dataset.theme === 'dark' ? 'vs-dark' : 'vs',
                mermaidTheme: root.dataset.theme,
                initialViewMode: 'split',
                uploadUrl: root.dataset.uploadUrl,
                onSave: function () { document.getElementById('server-form').requestSubmit(); },
                imageUploadOptions: {
                    onError: function () { document.getElementById('server-notes-upload-error').hidden = false; }
                }
            });
        } else {
            preview.innerHTML = ui.renderMarkdown(textarea.value);
            await ui.initializeMarkdownReader({ container: preview, mermaidTheme: root.dataset.theme });
        }
    } catch (error) {
        console.error('Server notes rendering failed:', error);
    }
});
