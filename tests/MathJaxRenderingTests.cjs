const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const project = path.resolve(__dirname, '../src/Aiursoft.EmployeeCenter');
const partial = fs.readFileSync(path.join(project, 'Views/Shared/_MarkdownReaderAssets.cshtml'), 'utf8');
const window = {};
vm.runInNewContext(partial.match(/<script>([\s\S]*?)<\/script>/)[1], { window });
const mathJax = require(path.join(project, 'wwwroot/node_modules/mathjax/es5/node-main.js')).init({
    ...window.MathJax,
    loader: { load: ['input/tex', 'output/chtml'] }
});

async function render(tex) {
    const mj = await mathJax;
    const document = mj.startup.getDocument(`<p><span class="math">\\(${tex}\\)</span></p>`);
    await mj._.util.Retries.handleRetriesFor(() => document.render());
    return mj.startup.adaptor.outerHTML(mj.startup.adaptor.body(document.document));
}

test('Markdown reader cannot create executable links or load unsafe TeX extensions', async () => {
    for (const tex of [
        String.raw`\href{javascript:alert(1)}{click}`,
        String.raw`\require{html}\href{javascript:alert(1)}{click}`,
        String.raw`\href{data:text/html,test}{click}`,
        String.raw`\htmlClass{unsafe}{click}`,
        String.raw`\style{color:red}{click}`
    ]) {
        const html = await render(tex);
        assert.doesNotMatch(html, /<a\b/i, tex);
        assert.match(html, /<mjx-merror\b/, tex);
    }
});

test('Markdown reader still renders ordinary and AMS equations', async () => {
    for (const tex of [
        String.raw`\frac{x^2}{2} + \sqrt{y}`,
        String.raw`\begin{aligned}x &= 1\\y &= 2\end{aligned}`
    ]) {
        const html = await render(tex);
        assert.match(html, /<mjx-container\b/, tex);
        assert.doesNotMatch(html, /<mjx-merror\b/, tex);
        assert.doesNotMatch(html, /<a\b/i, tex);
    }
});
