const content = document.getElementById('content');
const highlightTheme = document.getElementById('hljs-theme');
const knownThemes = new Set([
  'light',
  'dark',
  'sepia-light',
  'sepia-dark',
  'morning-light',
  'morning-dark',
  'lavender-light',
  'lavender-dark',
  'forest-light',
  'forest-dark',
  'nord-light',
  'nord-dark',
  'rose-light',
  'rose-dark',
]);
const darkThemes = new Set([
  'dark',
  'sepia-dark',
  'morning-dark',
  'lavender-dark',
  'forest-dark',
  'nord-dark',
  'rose-dark',
]);
let hljsApi = null;
let hljsLoadPromise = null;
let katexApi = null;
let katexLoadPromise = null;
let mermaidApi = null;
let mermaidLoadPromise = null;
let currentTheme = 'light';
let currentDocumentId = null;
let currentFontSize = 15;
let wheelAccumulator = 0;
let renderGeneration = 0;
let suppressScrollReport = false;
let scrollReportRaf = 0;
let pendingScroll = null;
let rendering = false;
let activeSearch = null;
let searchMatches = [];
let searchMatchIndex = -1;

const highlightCache = new Map();
const mathCache = new Map();
const mermaidCache = new Map();
const highlightObserver = new IntersectionObserver(
  (entries) => {
    for (const entry of entries) {
      if (!entry.isIntersecting) {
        continue;
      }

      const code = entry.target;
      highlightObserver.unobserve(code);
      void applyHighlight(code);
    }
  },
  { rootMargin: '400px' },
);

function trimCache(cache, limit) {
  while (cache.size > limit) {
    const oldest = cache.keys().next().value;
    if (oldest === undefined) {
      return;
    }

    cache.delete(oldest);
  }
}

function suppressProgrammaticScroll() {
  suppressScrollReport = true;
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      suppressScrollReport = false;
    });
  });
}

function captureScrollPercent() {
  const max = document.documentElement.scrollHeight - window.innerHeight;
  return max > 0 ? Math.max(0, Math.min(1, window.scrollY / max)) : 0;
}

function restoreScrollPercent(percent) {
  suppressProgrammaticScroll();
  const max = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
  window.scrollTo(0, Math.max(0, Math.min(1, percent)) * max);
}

function captureScrollPosition() {
  const max = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
  const percent = max > 0 ? Math.max(0, Math.min(1, window.scrollY / max)) : 0;
  const elements = content.querySelectorAll('[data-line]');
  if (elements.length === 0) {
    return { percent, line: 0 };
  }

  const scrollY = window.scrollY;
  const anchorY = scrollY + 24;
  let prevLine = 1;
  let prevTop = 0;
  let nextLine = null;
  let nextTop = null;

  for (const element of elements) {
    const elementLine = Number(element.dataset.line);
    if (!Number.isFinite(elementLine)) {
      continue;
    }

    const top = Math.max(0, element.getBoundingClientRect().top + scrollY - 24);
    if (top <= anchorY) {
      if (elementLine >= prevLine) {
        prevLine = elementLine;
        prevTop = top;
      }
    } else if (nextLine === null || elementLine < nextLine) {
      nextLine = elementLine;
      nextTop = top;
    }
  }

  let line = prevLine;
  if (nextLine !== null && nextTop !== null && nextLine > prevLine && nextTop > prevTop) {
    const ratio = Math.max(0, Math.min(1, (anchorY - prevTop) / (nextTop - prevTop)));
    line = prevLine + ratio * (nextLine - prevLine);
  }

  return { percent, line };
}

window.addEventListener('scroll', () => {
  if (rendering || suppressScrollReport || !currentDocumentId || scrollReportRaf) {
    return;
  }

  scrollReportRaf = requestAnimationFrame(() => {
    scrollReportRaf = 0;
    if (rendering || suppressScrollReport || !currentDocumentId) {
      return;
    }

    const position = captureScrollPosition();
    pendingScroll = {
      documentId: currentDocumentId,
      percent: position.percent,
      line: position.line,
    };
    send({
      type: 'scroll',
      documentId: currentDocumentId,
      percent: position.percent,
      line: position.line,
    });
  });
}, { passive: true });

function ensureMermaid() {
  if (mermaidApi) {
    return Promise.resolve(mermaidApi);
  }

  if (!mermaidLoadPromise) {
    mermaidLoadPromise = new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'mermaid.min.js';
      script.async = true;
      script.onload = () => {
        mermaidApi = window.mermaid ?? null;
        if (mermaidApi) {
          resolve(mermaidApi);
          return;
        }

        mermaidLoadPromise = null;
        reject(new Error('Mermaid 載入後無法使用。'));
      };
      script.onerror = () => {
        mermaidLoadPromise = null;
        reject(new Error('無法載入 Mermaid。'));
      };
      document.head.appendChild(script);
    });
  }

  return mermaidLoadPromise;
}

function ensureHljs() {
  if (hljsApi) {
    return Promise.resolve(hljsApi);
  }

  if (!hljsLoadPromise) {
    hljsLoadPromise = import('highlight.js/lib/common')
      .then((module) => {
        hljsApi = module.default ?? module;
        hljsApi.configure({ ignoreUnescapedHTML: true });
        return hljsApi;
      })
      .catch((error) => {
        hljsLoadPromise = null;
        throw error;
      });
  }

  return hljsLoadPromise;
}

function ensureKatex() {
  if (katexApi) {
    return Promise.resolve(katexApi);
  }

  if (!katexLoadPromise) {
    katexLoadPromise = import('katex')
      .then((module) => {
        katexApi = module.default ?? module;
        return katexApi;
      })
      .catch((error) => {
        katexLoadPromise = null;
        throw error;
      });
  }

  return katexLoadPromise;
}

function send(message) {
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage(message);
  }
}

function normalizeTheme(theme) {
  return knownThemes.has(theme) ? theme : 'light';
}

function setFontSize(size) {
  const value = Number(size);
  if (!Number.isFinite(value)) {
    return;
  }

  currentFontSize = Math.min(28, Math.max(10, value));
  document.documentElement.style.setProperty(
    '--preview-font-size',
    `${currentFontSize}px`);
}

function changeFontSize(delta) {
  const next = Math.min(28, Math.max(10, currentFontSize + delta));
  if (next === currentFontSize) {
    return;
  }

  setFontSize(next);
  send({ type: 'fontSize', size: next });
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

async function renderMath(root, generation) {
  const pending = [];
  for (const element of root.querySelectorAll('.math')) {
    if (element.dataset.mathRendered === 'true') {
      continue;
    }

    const source = stripMathDelimiters(element.textContent);
    if (!source) {
      continue;
    }

    pending.push({ element, source, displayMode: element.tagName === 'DIV' });
  }

  if (pending.length === 0) {
    return;
  }

  let katex = null;
  for (const item of pending) {
    if (generation !== renderGeneration || !item.element.isConnected) {
      return;
    }

    const cacheKey = (item.displayMode ? 'D' : 'I') + '\u0000' + item.source;
    let html = mathCache.get(cacheKey);

    if (!html) {
      try {
        katex ??= await ensureKatex();
        if (generation !== renderGeneration || !item.element.isConnected) {
          return;
        }

        html = katex.renderToString(item.source, {
          displayMode: item.displayMode,
          throwOnError: false,
          strict: 'ignore',
          trust: false,
        });
        mathCache.set(cacheKey, html);
        trimCache(mathCache, 200);
      } catch {
        continue;
      }
    }

    item.element.innerHTML = html;
    item.element.dataset.mathRendered = 'true';
    item.element.classList.add('math-rendered');
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

function createMermaidFigure(source, svg) {
  const figure = document.createElement('div');
  figure.className = 'mermaid-figure';
  figure.dataset.mermaidSource = source;
  figure.innerHTML = svg;

  const element = figure.querySelector('svg');
  if (element) {
    element.removeAttribute('height');
    element.style.maxWidth = '100%';
    element.style.height = 'auto';
  }

  return figure;
}

async function renderMermaidBlocks(blocks, generation) {
  if (blocks.length === 0) {
    return;
  }

  try {
    await ensureMermaid();
  } catch (error) {
    if (generation === renderGeneration) {
      for (const { holder, source } of blocks) {
        if (holder?.isConnected) {
          const errorBox = createMermaidError(source, error);
          if (holder.dataset.line) {
            errorBox.dataset.line = holder.dataset.line;
          }
          holder.replaceWith(errorBox);
        }
      }
    }

    return;
  }

  if (generation !== renderGeneration) {
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

    const cacheKey = currentTheme + '\u0000' + source;
    const cachedSvg = mermaidCache.get(cacheKey);

    if (cachedSvg) {
      const figure = createMermaidFigure(source, cachedSvg);
      if (holder.dataset.line) {
        figure.dataset.line = holder.dataset.line;
      }
      holder.replaceWith(figure);
      continue;
    }

    const renderId = `mermaid-${generation}-${index}`;
    try {
      const result = await mermaidApi.render(renderId, source);
      if (generation !== renderGeneration || !holder.isConnected) {
        continue;
      }

      mermaidCache.set(cacheKey, result.svg);
      trimCache(mermaidCache, 100);

      const figure = createMermaidFigure(source, result.svg);
      if (holder.dataset.line) {
        figure.dataset.line = holder.dataset.line;
      }
      holder.replaceWith(figure);
      if (typeof result.bindFunctions === 'function') {
        result.bindFunctions(figure);
      }
    } catch (error) {
      if (generation !== renderGeneration || !holder.isConnected) {
        continue;
      }

      const errorBox = createMermaidError(source, error);
      if (holder.dataset.line) {
        errorBox.dataset.line = holder.dataset.line;
      }
      holder.replaceWith(errorBox);
    } finally {
      document.getElementById(`d${renderId}`)?.remove();
    }
  }
}

async function applyHighlight(code) {
  if (code.dataset.highlighted || isMermaidCode(code)) {
    return;
  }

  const language = getCodeLanguage(code);
  const source = code.textContent ?? '';
  const cacheKey = language + '\u0000' + source;
  const cached = highlightCache.get(cacheKey);

  if (cached) {
    code.innerHTML = cached;
    code.classList.add('hljs');
    code.dataset.highlighted = 'yes';
    return;
  }

  try {
    const hljs = await ensureHljs();
    if (!code.isConnected || code.dataset.highlighted) {
      return;
    }

    hljs.highlightElement(code);
    code.dataset.highlighted = 'yes';
    highlightCache.set(cacheKey, code.innerHTML);
    trimCache(highlightCache, 200);
  } catch {
    // Rendering should still finish when highlighting fails.
  }
}

function highlightCodeBlocks() {
  for (const code of content.querySelectorAll('pre code')) {
    if (code.dataset.highlighted || isMermaidCode(code)) {
      continue;
    }

    highlightObserver.observe(code);
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

function enableTaskCheckboxes(root) {
  for (const checkbox of root.querySelectorAll('input[type="checkbox"]')) {
    checkbox.removeAttribute('disabled');
  }
}

function scrollToSourceLine(line, percent) {
  suppressProgrammaticScroll();
  const maxScroll = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
  if (maxScroll <= 0 || (percent ?? 0) <= 0) {
    window.scrollTo(0, 0);
    return;
  }

  const clampedPercent = Math.max(0, Math.min(1, percent ?? 0));
  if (clampedPercent >= 0.995) {
    window.scrollTo(0, maxScroll);
    return;
  }

  if (!Number.isFinite(line) || line <= 0) {
    window.scrollTo(0, clampedPercent * maxScroll);
    return;
  }

  const elements = content.querySelectorAll('[data-line]');
  if (elements.length === 0) {
    window.scrollTo(0, clampedPercent * maxScroll);
    return;
  }

  let prevLine = 1;
  let prevTop = 0;
  let nextLine = null;
  let nextTop = null;
  const scrollY = window.scrollY;

  for (const element of elements) {
    const elementLine = Number(element.dataset.line);
    if (!Number.isFinite(elementLine)) {
      continue;
    }

    const top = Math.max(0, element.getBoundingClientRect().top + scrollY - 24);
    if (elementLine <= line) {
      if (elementLine >= prevLine) {
        prevLine = elementLine;
        prevTop = top;
      }
    } else if (nextLine === null || elementLine < nextLine) {
      nextLine = elementLine;
      nextTop = top;
    }
  }

  let targetY;
  if (nextLine !== null && nextTop !== null && nextLine > prevLine) {
    const ratio = (line - prevLine) / (nextLine - prevLine);
    targetY = prevTop + ratio * (nextTop - prevTop);
  } else {
    targetY = prevTop;
  }

  if (clampedPercent > 0.9) {
    const blend = (clampedPercent - 0.9) / 0.1;
    targetY = targetY * (1 - blend) + maxScroll * blend;
  }

  window.scrollTo(0, Math.max(0, Math.min(maxScroll, targetY)));
}

async function renderContent(data) {
  const generation = ++renderGeneration;
  const sameDocument = Boolean(data.documentId) && data.documentId === currentDocumentId;
  const previousScroll = captureScrollPercent();
  currentDocumentId = data.documentId ?? null;
  rendering = true;

  if (pendingScroll && pendingScroll.documentId !== currentDocumentId) {
    pendingScroll = null;
  }

  highlightObserver.disconnect();

  try {
    content.innerHTML = data.html ?? '';
    setTheme(data.theme);
    enableTaskCheckboxes(content);
    await renderMath(content, generation);

    if (generation !== renderGeneration) {
      return;
    }

    if (sameDocument) {
      restoreScrollPercent(previousScroll);
    } else {
      scrollToSourceLine(
        Number(data.previewScrollLine),
        Number(data.previewScrollPercent));
    }

    const mermaidBlocks = collectMermaidBlocks(content);

    if (generation === renderGeneration) {
      await renderMermaidBlocks(mermaidBlocks, generation);
    }

    if (generation === renderGeneration) {
      applyEnhancements();

      if (sameDocument) {
        restoreScrollPercent(previousScroll);
      } else {
        scrollToSourceLine(
          Number(data.previewScrollLine),
          Number(data.previewScrollPercent));
      }

      if (pendingScroll && pendingScroll.documentId === currentDocumentId) {
        scrollToSourceLine(pendingScroll.line, pendingScroll.percent);
      }

      if (activeSearch) {
        applyPreviewSearch(activeSearch, false, true);
      }
    }
  } finally {
    if (generation === renderGeneration) {
      suppressProgrammaticScroll();
      rendering = false;
    }

    send({ type: 'rendered', id: data.id });
  }
}

async function rerenderMermaid() {
  const blocks = collectMermaidBlocks(content);
  if (blocks.length === 0) {
    return;
  }

  const generation = ++renderGeneration;
  const previousScroll = captureScrollPercent();
  await renderMermaidBlocks(blocks, generation);

  if (generation === renderGeneration) {
    applyEnhancements();
    restoreScrollPercent(previousScroll);
  }
}

async function preparePrint() {
  highlightObserver.disconnect();
  clearPreviewSearchMarks();

  for (const code of content.querySelectorAll('pre code')) {
    if (isMermaidCode(code)) {
      continue;
    }

    await applyHighlight(code);
  }

  await waitForImages();
  await new Promise((resolve) => requestAnimationFrame(() => resolve()));
}

async function buildExportHtml() {
  for (const code of content.querySelectorAll('pre code')) {
    if (!isMermaidCode(code)) {
      await applyHighlight(code);
    }
  }

  const clone = content.cloneNode(true);

  for (const button of clone.querySelectorAll('.code-copy')) {
    button.remove();
  }

  for (const checkbox of clone.querySelectorAll('input[type="checkbox"]')) {
    checkbox.disabled = true;
  }

  for (const element of clone.querySelectorAll('[data-line]')) {
    element.removeAttribute('data-line');
  }

  for (const mark of clone.querySelectorAll('mark.md-search-match')) {
    const parent = mark.parentNode;
    if (parent) {
      parent.replaceChild(document.createTextNode(mark.textContent ?? ''), mark);
      parent.normalize();
    }
  }

  return clone.innerHTML;
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function buildPreviewSearchRegex(options) {
  if (!options || !options.query) {
    return null;
  }

  let source = options.regexp ? options.query : escapeRegExp(options.query);
  if (options.wholeWord) {
    source = `\\b(?:${source})\\b`;
  }

  try {
    return new RegExp(source, options.caseSensitive ? 'g' : 'gi');
  } catch {
    return null;
  }
}

function clearPreviewSearchMarks() {
  const parents = new Set();
  for (const mark of content.querySelectorAll('mark.md-search-match')) {
    const parent = mark.parentNode;
    if (!parent) {
      continue;
    }

    parent.replaceChild(document.createTextNode(mark.textContent ?? ''), mark);
    parents.add(parent);
  }

  for (const parent of parents) {
    parent.normalize();
  }

  searchMatches = [];
  searchMatchIndex = -1;
}

function reportPreviewSearchResult(validRegex = true) {
  send({
    type: 'searchResult',
    documentId: currentDocumentId,
    currentIndex: searchMatchIndex >= 0 ? searchMatchIndex + 1 : 0,
    totalCount: searchMatches.length,
    validRegex,
  });
}

function highlightPreviewMatch(index, shouldScroll) {
  if (searchMatches.length === 0) {
    searchMatchIndex = -1;
    return;
  }

  if (searchMatchIndex >= 0 && searchMatchIndex < searchMatches.length) {
    searchMatches[searchMatchIndex].classList.remove('md-search-match-current');
  }

  searchMatchIndex = ((index % searchMatches.length) + searchMatches.length) % searchMatches.length;
  const target = searchMatches[searchMatchIndex];
  target.classList.add('md-search-match-current');

  if (shouldScroll) {
    suppressProgrammaticScroll();
    target.scrollIntoView({ block: 'center', behavior: 'smooth' });
  }
}

function applyPreviewSearch(data, shouldScroll, preserveIndex = false) {
  const previousIndex = searchMatchIndex;
  clearPreviewSearchMarks();

  const queryText = typeof data?.query === 'string' ? data.query : '';
  if (!queryText) {
    activeSearch = null;
    reportPreviewSearchResult(true);
    return;
  }

  activeSearch = {
    query: queryText,
    caseSensitive: Boolean(data.caseSensitive),
    wholeWord: Boolean(data.wholeWord),
    regexp: Boolean(data.regexp),
  };

  const regex = buildPreviewSearchRegex(activeSearch);
  if (!regex) {
    reportPreviewSearchResult(false);
    return;
  }

  const walker = document.createTreeWalker(
    content,
    NodeFilter.SHOW_TEXT,
    {
      acceptNode(node) {
        if (!node.nodeValue || !node.nodeValue.trim()) {
          return NodeFilter.FILTER_SKIP;
        }

        const parent = node.parentElement;
        if (!parent) {
          return NodeFilter.FILTER_REJECT;
        }

        if (parent.closest('script, style, .code-copy, .katex-mathml, mark.md-search-match')) {
          return NodeFilter.FILTER_REJECT;
        }

        return NodeFilter.FILTER_ACCEPT;
      },
    },
  );

  const textNodes = [];
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    textNodes.push(node);
  }

  const maxMatches = 5000;
  for (const textNode of textNodes) {
    if (searchMatches.length >= maxMatches) {
      break;
    }

    const text = textNode.nodeValue ?? '';
    regex.lastIndex = 0;
    let match = regex.exec(text);
    if (!match) {
      continue;
    }

    const fragment = document.createDocumentFragment();
    let lastEnd = 0;

    while (match && searchMatches.length < maxMatches) {
      if (match[0].length === 0) {
        regex.lastIndex += 1;
        match = regex.exec(text);
        continue;
      }

      if (match.index > lastEnd) {
        fragment.appendChild(document.createTextNode(text.slice(lastEnd, match.index)));
      }

      const mark = document.createElement('mark');
      mark.className = 'md-search-match';
      mark.textContent = match[0];
      fragment.appendChild(mark);
      searchMatches.push(mark);

      lastEnd = match.index + match[0].length;
      match = regex.exec(text);
    }

    if (lastEnd < text.length) {
      fragment.appendChild(document.createTextNode(text.slice(lastEnd)));
    }

    textNode.parentNode?.replaceChild(fragment, textNode);
  }

  if (searchMatches.length > 0) {
    const nextIndex = preserveIndex && previousIndex >= 0
      ? Math.min(previousIndex, searchMatches.length - 1)
      : 0;
    highlightPreviewMatch(nextIndex, shouldScroll);
  }

  reportPreviewSearchResult(true);
}

function clearPreviewSearch() {
  activeSearch = null;
  clearPreviewSearchMarks();
  reportPreviewSearchResult(true);
}

function stepPreviewSearch(delta) {
  if (searchMatches.length === 0) {
    reportPreviewSearchResult(true);
    return;
  }

  const start = searchMatchIndex < 0 ? (delta >= 0 ? 0 : searchMatches.length - 1) : searchMatchIndex + delta;
  highlightPreviewMatch(start, true);
  reportPreviewSearchResult(true);
}

function getPreviewSingleLineSelection() {
  const text = window.getSelection()?.toString() ?? '';
  if (!text || text.includes('\n') || text.includes('\r') || text.length > 200) {
    return '';
  }

  return text;
}

window.chrome?.webview?.addEventListener('message', async (event) => {
  const data = event.data;
  if (!data || typeof data.type !== 'string') {
    return;
  }

  switch (data.type) {
    case 'configure':
      if (typeof data.theme === 'string') {
        setTheme(data.theme);
      }
      if (data.fontSize !== undefined) {
        setFontSize(data.fontSize);
      }
      break;
    case 'render':
      await renderContent(data);
      break;
    case 'theme':
      setTheme(data.theme);
      await rerenderMermaid();
      break;
    case 'font':
      setFontSize(data.size);
      break;
    case 'scroll': {
      pendingScroll = {
        documentId: data.documentId || currentDocumentId,
        percent: data.percent,
        line: data.line,
      };

      if (data.documentId && data.documentId !== currentDocumentId) {
        break;
      }

      scrollToSourceLine(Number(data.line), data.percent);
      break;
    }
    case 'exportHtml':
      send({ type: 'exportHtml', id: data.id, html: await buildExportHtml() });
      break;
    case 'preparePrint':
      await preparePrint();
      send({ type: 'printReady', id: data.id });
      break;
    case 'search':
      applyPreviewSearch(data, Boolean(data.jumpToFirst), false);
      break;
    case 'findNext':
      stepPreviewSearch(1);
      break;
    case 'findPrevious':
      stepPreviewSearch(-1);
      break;
    case 'clearSearch':
      clearPreviewSearch();
      break;
    case 'getSelectedText':
      send({
        type: 'selectedText',
        id: data.id,
        text: getPreviewSingleLineSelection(),
      });
      break;
    case 'focus':
      window.focus();
      break;
    default:
      break;
  }
});

window.addEventListener('keydown', (event) => {
  if (event.repeat) {
    return;
  }

  if (!event.ctrlKey && !event.altKey) {
    if (event.key === 'F3') {
      event.preventDefault();
      send({ type: 'shortcut', name: event.shiftKey ? 'findPrevious' : 'findNext' });
      return;
    }

    if (event.key === 'Escape' && activeSearch) {
      event.preventDefault();
      send({ type: 'shortcut', name: 'closeSearch' });
      return;
    }

    return;
  }

  if (!event.ctrlKey || event.altKey) {
    return;
  }

  const key = event.key.toLowerCase();
  let name = null;

  if (key === 'tab') {
    name = event.shiftKey ? 'previousTab' : 'nextTab';
  } else if (event.shiftKey) {
    if (key === 's') {
      name = 'saveAs';
    } else if (key === 't') {
      name = 'reopenClosed';
    } else if (key === '+' || key === '=') {
      name = 'zoomIn';
    }
  } else {
    name = {
      '0': 'zoomReset',
      '1': 'viewEdit',
      '2': 'viewPreview',
      '3': 'viewSplit',
      ',': 'preferences',
      '-': 'zoomOut',
      '=': 'zoomIn',
      f: 'find',
      h: 'replace',
      n: 'new',
      o: 'open',
      s: 'save',
      w: 'close',
    }[key] ?? null;
  }

  if (name) {
    event.preventDefault();
    send({ type: 'shortcut', name });
  }
});

window.addEventListener('wheel', (event) => {
  if (!event.ctrlKey || event.deltaY === 0) {
    return;
  }

  event.preventDefault();

  if ((wheelAccumulator > 0 && event.deltaY < 0) ||
      (wheelAccumulator < 0 && event.deltaY > 0)) {
    wheelAccumulator = 0;
  }

  wheelAccumulator += event.deltaY;
  if (Math.abs(wheelAccumulator) < 40) {
    return;
  }

  const direction = wheelAccumulator < 0 ? 1 : -1;
  wheelAccumulator = 0;
  changeFontSize(direction);
}, { passive: false });

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

document.addEventListener('contextmenu', (event) => {
  const target = event.target instanceof Element ? event.target : null;
  const anchor = target?.closest('a') ?? null;
  const image = target?.closest('img') ?? null;

  window.__mdeditorContextTarget = {
    link: anchor?.getAttribute('href') ?? null,
    image: image?.getAttribute('src') ?? null,
    selection: window.getSelection()?.toString() ?? '',
  };
});

document.addEventListener('change', (event) => {
  const checkbox = event.target;
  if (!(checkbox instanceof HTMLInputElement) || checkbox.type !== 'checkbox') {
    return;
  }

  const item = checkbox.closest('li[data-line], [data-line]');
  const line = Number(item?.dataset.line);
  if (!Number.isInteger(line) || line < 1) {
    return;
  }

  send({
    type: 'toggleTask',
    documentId: currentDocumentId,
    line,
    checked: checkbox.checked,
  });
});

document.documentElement.dataset.theme = currentTheme;
send({ type: 'ready' });
