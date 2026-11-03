import hljs from 'highlight.js/lib/common';

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
let currentTheme = 'light';

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

function enhanceCodeBlocks() {
  const blocks = Array.from(content.querySelectorAll('pre'));
  for (const pre of blocks) {
    if (pre.parentElement?.classList.contains('code-block')) {
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

window.chrome?.webview?.addEventListener('message', async (event) => {
  const data = event.data;
  if (!data || typeof data.type !== 'string') {
    return;
  }

  switch (data.type) {
    case 'render': {
      content.innerHTML = data.html ?? '';
      removeTitleAttributes();
      setTheme(data.theme);
      await waitForImages();
      try {
        hljs.highlightAll();
      } catch {
        // Rendering should still finish when highlighting fails.
      }
      enhanceCodeBlocks();
      send({ type: 'rendered', id: data.id });
      break;
    }
    case 'theme':
      setTheme(data.theme);
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
