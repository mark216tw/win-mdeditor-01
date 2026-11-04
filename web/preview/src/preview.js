import hljs from 'highlight.js/lib/common';
import katex from 'katex';

const content = document.getElementById('content');
const highlightTheme = document.getElementById('hljs-theme');
const knownThemes = new Set([
  'light',
  'dark',
  'sepia',
  'morning',
  'lavender',
  'nord',
  'forest',
  'highcontrast',
]);
const darkThemes = new Set(['dark', 'nord', 'forest', 'highcontrast']);
const mermaidApi = window.mermaid;
let currentTheme = 'light';
let renderGeneration = 0;

hljs.configure({ ignoreUnescapedHTML: true });

function send(message) {
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage(message);
  }
}

function normalizeTheme(theme) {
  return knownThemes.has(theme) ? theme : 'light';
}

function setTheme(theme) {
  currentTheme = normalizeTheme(theme);
  document.documentElement.dataset.theme = currentTheme;
  highlightTheme.href = darkThemes.has(currentTheme) ? 'github-dark.min.css' : 'github.min.css';
}

function getCssColor(name, fallback) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

function removeTitleAttributes() {
  content.querySelectorAll('[title]').forEach((element) => element.removeAttribute('title'));
}

async function copyText(text) {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return;
    } catch {
      // Fall through to the legacy clipboard path.
    }
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();

  try {
    document.execCommand('copy');
  } finally {
    textarea.remove();
  }
}

function stripMathDelimiters(text) {
  let value = (text ?? '').trim();

  if (value.startsWith('\\(') && value.endsWith('\\)')) {
    return value.slice(2, -2).trim();
  }

  if (value.startsWith('\\[') && value.endsWith('\\]')) {
    return value.slice(2, -2).trim();
  }

  if (value.startsWith('$$') && value.endsWith('$$')) {
    return value.slice(2, -2).trim();
  }

  if (value.startsWith('$') && value.endsWith('$')) {
    return value.slice(1, -1).trim();
  }

  return value;
}

function renderMath(root) {
  for (const element of root.querySelectorAll('.math')) {
    if (element.dataset.mathRendered === 'true') {
      continue;
    }

    const source = stripMathDelimiters(element.textContent);
    if (!source) {
      continue;
    }

    try {
      katex.render(source, element, {
        displayMode: element.tagName === 'DIV',
        throwOnError: false,
        strict: 'ignore',
        trust: false,
      });
      element.dataset.mathRendered = 'true';
      element.classList.add('math-rendered');
    } catch {
      // Keep the original LaTeX text when rendering fails.
    }
  }
}

function getCodeLanguage(code) {
  for (const name of code?.classList ?? []) {
    if (name.startsWith('language-') || name.startsWith('lang-')) {
      return name.replace(/^(language-|lang-)/, '').toLowerCase();
    }
  }

  return '';
}

function isMermaidCode(code) {
  return Boolean(
    code &&
    (code.classList.contains('mermaid') ||
      code.parentElement?.classList.contains('mermaid') ||
      getCodeLanguage(code) === 'mermaid'),
  );
}

function collectMermaidBlocks(root) {
  const blocks = [];
  const holders = new Set();

  for (const pre of root.querySelectorAll('pre')) {
    const code = pre.querySelector('code');
    if (!isMermaidCode(code) && !pre.classList.contains('mermaid')) {
      continue;
    }

    holders.add(pre);
    blocks.push({ holder: pre, source: (code ?? pre).textContent ?? '' });
  }

  for (const div of root.querySelectorAll('div.mermaid')) {
    if (div.closest('pre') || holders.has(div)) {
      continue;
    }

    holders.add(div);
    blocks.push({ holder: div, source: div.textContent ?? '' });
  }

  for (const holder of root.querySelectorAll('.mermaid-figure, .mermaid-error')) {
    if (holders.has(holder) || !holder.dataset.mermaidSource) {
      continue;
    }

    holders.add(holder);
    blocks.push({ holder, source: holder.dataset.mermaidSource });
  }

  return blocks;
}

function configureMermaid() {
  if (!mermaidApi) {
    return;
  }

  const dark = darkThemes.has(currentTheme);
  const foreground = getCssColor('--fg', dark ? '#e6edf3' : '#1f2328');
  const border = getCssColor('--border', dark ? '#3d444d' : '#d1d9e0');
  const muted = getCssColor('--muted', dark ? '#9198a1' : '#59636e');
  const surface = getCssColor('--quote-bg', dark ? '#161b22' : '#f6f8fa');

  mermaidApi.initialize({
    startOnLoad: false,
    securityLevel: 'strict',
    logLevel: 'error',
    theme: 'base',
    fontFamily: '"Segoe UI", "Microsoft JhengHei", "Noto Sans TC", system-ui, sans-serif',
    themeVariables: {
      darkMode: dark,
      background: 'transparent',
      primaryColor: surface,
      primaryTextColor: foreground,
      primaryBorderColor: border,
      lineColor: muted,
      secondaryColor: surface,
      tertiaryColor: getCssColor('--bg', dark ? '#0d1117' : '#ffffff'),
      textColor: foreground,
      mainBkg: surface,
      nodeBorder: border,
      clusterBkg: surface,
      clusterBorder: border,
      edgeLabelBackground: getCssColor('--bg', dark ? '#0d1117' : '#ffffff'),
    },
    flowchart: {
      htmlLabels: false,
      useMaxWidth: true,
    },
    sequence: {
      useMaxWidth: true,
    },
    gantt: {
      useMaxWidth: true,
    },
  });
}

function createMermaidError(source, error) {
  const box = document.createElement('div');
  box.className = 'mermaid-error';
  box.dataset.mermaidSource = source;

  const title = document.createElement('strong');
  title.textContent = '流程圖無法繪製';

  const message = document.createElement('p');
  message.textContent = error instanceof Error ? error.message : String(error ?? '語法錯誤');

  const pre = document.createElement('pre');
  const code = document.createElement('code');
  code.textContent = source;
  pre.appendChild(code);

  box.append(title, message, pre);
  return box;
}

async function renderMermaidBlocks(blocks, generation) {
  if (!mermaidApi || blocks.length === 0) {
    return;
  }

  configureMermaid();

  for (let index = 0; index < blocks.length; index++) {
    if (generation !== renderGeneration) {
      return;
    }

    const { holder, source } = blocks[index];
    if (!holder?.isConnected || !source.trim()) {
      continue;
    }

    const renderId = `mermaid-${generation}-${index}`;
    try {
      const result = await mermaidApi.render(renderId, source);
      if (generation !== renderGeneration || !holder.isConnected) {
        continue;
      }

      const figure = document.createElement('div');
      figure.className = 'mermaid-figure';
      figure.dataset.mermaidSource = source;
      figure.innerHTML = result.svg;

      const svg = figure.querySelector('svg');
      if (svg) {
        svg.removeAttribute('height');
        svg.style.maxWidth = '100%';
        svg.style.height = 'auto';
      }

      holder.replaceWith(figure);
      if (typeof result.bindFunctions === 'function') {
        result.bindFunctions(figure);
      }
    } catch (error) {
      if (generation !== renderGeneration || !holder.isConnected) {
        continue;
      }

      holder.replaceWith(createMermaidError(source, error));
    } finally {
      document.getElementById(`d${renderId}`)?.remove();
    }
  }
}

function highlightCodeBlocks() {
  for (const code of content.querySelectorAll('pre code')) {
    if (code.dataset.highlighted || isMermaidCode(code)) {
      continue;
    }

    try {
      hljs.highlightElement(code);
    } catch {
      // Rendering should still finish when highlighting fails.
    }
  }
}

function enhanceCodeBlocks() {
  const blocks = Array.from(content.querySelectorAll('pre'));
  for (const pre of blocks) {
    if (
      pre.parentElement?.classList.contains('code-block') ||
      pre.classList.contains('mermaid') ||
      pre.closest('.mermaid-figure, .mermaid-error')
    ) {
      continue;
    }

    const wrapper = document.createElement('div');
    wrapper.className = 'code-block';
    pre.parentNode.insertBefore(wrapper, pre);
    wrapper.appendChild(pre);

    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'code-copy';
    button.textContent = '複製';
    button.setAttribute('aria-label', '複製程式碼');
    button.addEventListener('click', async () => {
      const text = pre.querySelector('code')?.textContent ?? pre.textContent ?? '';
      await copyText(text);
      button.textContent = '已複製';
      button.classList.add('copied');
      setTimeout(() => {
        button.textContent = '複製';
        button.classList.remove('copied');
      }, 1500);
    });

    wrapper.appendChild(button);
  }
}

function applyEnhancements() {
  removeTitleAttributes();
  highlightCodeBlocks();
  enhanceCodeBlocks();
}

function waitForImages() {
  const images = Array.from(content.querySelectorAll('img'));
  return Promise.all(images.map((image) => {
    if (image.complete) {
      return Promise.resolve();
    }

    return new Promise((resolve) => {
      image.addEventListener('load', resolve, { once: true });
      image.addEventListener('error', resolve, { once: true });
      setTimeout(resolve, 1500);
    });
  }));
}

async function renderContent(data) {
  const generation = ++renderGeneration;

  try {
    content.innerHTML = data.html ?? '';
    setTheme(data.theme);
    renderMath(content);

    const mermaidBlocks = collectMermaidBlocks(content);

    await waitForImages();
    if (generation === renderGeneration) {
      await renderMermaidBlocks(mermaidBlocks, generation);
    }

    if (generation === renderGeneration) {
      applyEnhancements();
    }
  } finally {
    send({ type: 'rendered', id: data.id });
  }
}

async function rerenderMermaid() {
  const blocks = collectMermaidBlocks(content);
  if (blocks.length === 0) {
    return;
  }

  const generation = ++renderGeneration;
  await renderMermaidBlocks(blocks, generation);

  if (generation === renderGeneration) {
    applyEnhancements();
  }
}

window.chrome?.webview?.addEventListener('message', async (event) => {
  const data = event.data;
  if (!data || typeof data.type !== 'string') {
    return;
  }

  switch (data.type) {
    case 'render':
      await renderContent(data);
      break;
    case 'theme':
      setTheme(data.theme);
      await rerenderMermaid();
      break;
    case 'scroll': {
      const max = document.documentElement.scrollHeight - window.innerHeight;
      window.scrollTo(0, Math.max(0, Math.min(1, data.percent ?? 0)) * max);
      break;
    }
    default:
      break;
  }
});

document.addEventListener('click', (event) => {
  const anchor = event.target.closest('a');
  if (!anchor) {
    return;
  }

  const href = anchor.getAttribute('href');
  if (!href) {
    return;
  }

  if (href.startsWith('#')) {
    const target = document.getElementById(decodeURIComponent(href.slice(1)));
    if (target) {
      event.preventDefault();
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    return;
  }

  event.preventDefault();
  send({ type: 'link', href });
});

document.documentElement.dataset.theme = currentTheme;
send({ type: 'ready' });
