import { basicSetup } from 'codemirror';
import { EditorView, keymap } from '@codemirror/view';
import { EditorState, Compartment } from '@codemirror/state';
import { indentWithTab } from '@codemirror/commands';
import { markdown, markdownLanguage } from '@codemirror/lang-markdown';
import { syntaxHighlighting } from '@codemirror/language';
import { languages } from '@codemirror/language-data';
import { oneDarkHighlightStyle } from '@codemirror/theme-one-dark';

const themeCompartment = new Compartment();
const wrapCompartment = new Compartment();

const chromeThemeSpec = {
  '&': {
    backgroundColor: 'var(--cm-bg)',
    color: 'var(--cm-fg)',
  },
  '.cm-content': {
    caretColor: 'var(--cm-caret)',
  },
  '.cm-cursor, .cm-dropCursor': {
    borderLeftColor: 'var(--cm-caret)',
  },
  '.cm-gutters': {
    backgroundColor: 'var(--cm-gutter-bg)',
    color: 'var(--cm-gutter-fg)',
    border: 'none',
  },
  '.cm-activeLine': {
    backgroundColor: 'var(--cm-active-line)',
  },
  '.cm-activeLineGutter': {
    backgroundColor: 'var(--cm-active-line)',
  },
  '.cm-selectionBackground, ::selection': {
    backgroundColor: 'var(--cm-selection) !important',
  },
  '&.cm-focused .cm-selectionBackground': {
    backgroundColor: 'var(--cm-selection) !important',
  },
};

const fontTheme = EditorView.theme({
  '&': {
    height: '100%',
    fontSize: 'var(--editor-font-size, 14px)',
  },
  '.cm-scroller': {
    fontFamily: 'var(--editor-font-family, "Cascadia Mono", Consolas, monospace)',
    lineHeight: '1.65',
  },
  '.cm-content': {
    padding: '12px 0 72px',
  },
  '.cm-line': {
    padding: '0 16px',
  },
});

const lightTheme = EditorView.theme(chromeThemeSpec, { dark: false });
const darkChromeTheme = EditorView.theme(chromeThemeSpec, { dark: true });
const lightThemeExtensions = [lightTheme, fontTheme];
const darkThemeExtensions = [
  syntaxHighlighting(oneDarkHighlightStyle),
  darkChromeTheme,
  fontTheme,
];

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

function normalizeTheme(theme) {
  return knownThemes.has(theme) ? theme : 'light';
}

function themeExtensions(theme) {
  return darkThemes.has(theme) ? darkThemeExtensions : lightThemeExtensions;
}

let currentTheme = 'light';
let currentWordWrap = true;
let currentDocumentId = null;
let suppressChange = false;
let changeTimer = null;
let selectionTimer = null;
let scrollTimer = null;

function send(message) {
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage(message);
  }
}

function buildExtensions() {
  return [
    basicSetup,
    keymap.of([
      indentWithTab,
      {
        key: 'Mod-s',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'save' });
          return true;
        },
      },
      {
        key: 'Mod-Shift-s',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'saveAs' });
          return true;
        },
      },
      {
        key: 'Mod-o',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'open' });
          return true;
        },
      },
      {
        key: 'Mod-n',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'new' });
          return true;
        },
      },
      {
        key: 'Mod-w',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'close' });
          return true;
        },
      },
      {
        key: 'Mod-,',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'preferences' });
          return true;
        },
      },
      {
        key: 'Mod-b',
        preventDefault: true,
        run: () => {
          applyCommand('bold');
          return true;
        },
      },
      {
        key: 'Mod-i',
        preventDefault: true,
        run: () => {
          applyCommand('italic');
          return true;
        },
      },
      {
        key: 'Mod-1',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'viewEdit' });
          return true;
        },
      },
      {
        key: 'Mod-2',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'viewPreview' });
          return true;
        },
      },
      {
        key: 'Mod-3',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'viewSplit' });
          return true;
        },
      },
    ]),
    markdown({ base: markdownLanguage, codeLanguages: languages }),
    EditorState.tabSize.of(4),
    themeCompartment.of(themeExtensions(currentTheme)),
    wrapCompartment.of(currentWordWrap ? EditorView.lineWrapping : []),
    EditorView.updateListener.of((update) => {
      if (suppressChange) {
        return;
      }

      if (update.docChanged) {
        clearTimeout(changeTimer);
        changeTimer = setTimeout(() => {
          send({
            type: 'change',
            documentId: currentDocumentId,
            text: view.state.doc.toString(),
          });
        }, 150);
      }

      if (update.docChanged || update.selectionSet) {
        clearTimeout(selectionTimer);
        selectionTimer = setTimeout(sendSelection, 80);
      }
    }),
  ];
}

const view = new EditorView({
  state: EditorState.create({
    doc: '',
    extensions: buildExtensions(),
  }),
  parent: document.getElementById('editor'),
});

function sendSelection() {
  const selection = view.state.selection.main;
  const line = view.state.doc.lineAt(selection.head);
  send({
    type: 'selection',
    documentId: currentDocumentId,
    line: line.number,
    column: selection.head - line.from + 1,
    selectionLength: selection.to - selection.from,
  });
}

function sendScroll() {
  const scroller = view.scrollDOM;
  const max = scroller.scrollHeight - scroller.clientHeight;
  send({
    type: 'scroll',
    documentId: currentDocumentId,
    percent: max > 0 ? scroller.scrollTop / max : 0,
  });
}

view.scrollDOM.addEventListener('scroll', () => {
  clearTimeout(scrollTimer);
  scrollTimer = setTimeout(sendScroll, 100);
});

function setContent(text, documentId) {
  currentDocumentId = documentId;
  suppressChange = true;
  clearTimeout(changeTimer);
  view.setState(EditorState.create({
    doc: text ?? '',
    extensions: buildExtensions(),
  }));
  suppressChange = false;
  view.scrollDOM.scrollTop = 0;
  sendSelection();
  sendScroll();
}

function setTheme(theme) {
  currentTheme = normalizeTheme(theme);
  document.documentElement.dataset.theme = currentTheme;
  view.dispatch({
    effects: themeCompartment.reconfigure(themeExtensions(currentTheme)),
  });
}

function setWordWrap(enabled) {
  currentWordWrap = Boolean(enabled);
  view.dispatch({
    effects: wrapCompartment.reconfigure(
      currentWordWrap ? EditorView.lineWrapping : []),
  });
}

function currentSelection() {
  const selection = view.state.selection.main;
  return {
    selection,
    text: view.state.sliceDoc(selection.from, selection.to),
  };
}

function wrapInline(before, after, placeholder) {
  const { selection, text } = currentSelection();
  const inner = text || placeholder;
  const insert = before + inner + after;
  view.dispatch({
    changes: { from: selection.from, to: selection.to, insert },
    selection: {
      anchor: selection.from + before.length,
      head: selection.from + before.length + inner.length,
    },
  });
  view.focus();
}

function toggleHeading(level) {
  const { selection } = currentSelection();
  const first = view.state.doc.lineAt(selection.from).number;
  const last = view.state.doc.lineAt(selection.to).number;
  const lines = [];
  for (let number = first; number <= last; number += 1) {
    lines.push(view.state.doc.line(number));
  }

  const matcher = /^(#{1,6})\s+/;
  const allMatch = lines.every((line) => {
    const match = line.text.match(matcher);
    return match && match[1].length === level;
  });

  const changes = [];
  for (const line of lines) {
    const match = line.text.match(matcher);
    if (allMatch) {
      changes.push({ from: line.from, to: line.from + match[0].length, insert: '' });
    } else if (match) {
      changes.push({
        from: line.from,
        to: line.from + match[0].length,
        insert: '#'.repeat(level) + ' ',
      });
    } else {
      changes.push({ from: line.from, insert: '#'.repeat(level) + ' ' });
    }
  }

  view.dispatch({ changes });
  view.focus();
}

function togglePrefix(prefix) {
  const { selection } = currentSelection();
  const first = view.state.doc.lineAt(selection.from).number;
  const last = view.state.doc.lineAt(selection.to).number;
  const lines = [];
  for (let number = first; number <= last; number += 1) {
    lines.push(view.state.doc.line(number));
  }

  const allHave = lines.every((line) => line.text.startsWith(prefix));
  const changes = [];
  for (const line of lines) {
    if (allHave) {
      changes.push({ from: line.from, to: line.from + prefix.length, insert: '' });
    } else if (!line.text.startsWith(prefix)) {
      changes.push({ from: line.from, insert: prefix });
    }
  }

  view.dispatch({ changes });
  view.focus();
}

function insertSnippet(text, cursorBack) {
  const { selection } = currentSelection();
  view.dispatch({
    changes: { from: selection.from, to: selection.to, insert: text },
    selection: { anchor: selection.from + text.length - (cursorBack ?? 0) },
  });
  view.focus();
}

function insertLink() {
  const { selection, text } = currentSelection();
  const label = text || '連結文字';
  const insert = `[${label}](https://)`;
  const urlStart = selection.from + 1 + label.length + 2;
  view.dispatch({
    changes: { from: selection.from, to: selection.to, insert },
    selection: { anchor: urlStart, head: urlStart + 8 },
  });
  view.focus();
}

function insertImage() {
  const { selection, text } = currentSelection();
  const label = text || '替代文字';
  const insert = `![${label}](images/)`;
  const pathStart = selection.from + 2 + label.length + 2;
  view.dispatch({
    changes: { from: selection.from, to: selection.to, insert },
    selection: { anchor: pathStart, head: pathStart + 7 },
  });
  view.focus();
}

function applyCommand(name) {
  switch (name) {
    case 'bold':
      wrapInline('**', '**', '粗體文字');
      break;
    case 'italic':
      wrapInline('*', '*', '斜體文字');
      break;
    case 'strike':
      wrapInline('~~', '~~', '刪除線文字');
      break;
    case 'inlinecode':
      wrapInline('`', '`', '程式碼');
      break;
    case 'h1':
      toggleHeading(1);
      break;
    case 'h2':
      toggleHeading(2);
      break;
    case 'h3':
      toggleHeading(3);
      break;
    case 'ul':
      togglePrefix('- ');
      break;
    case 'ol':
      togglePrefix('1. ');
      break;
    case 'task':
      togglePrefix('- [ ] ');
      break;
    case 'quote':
      togglePrefix('> ');
      break;
    case 'code': {
      const { selection, text } = currentSelection();
      const body = text || '';
      const snippet = '\n```\n' + body + '\n```\n';
      insertSnippet(snippet, text ? 0 : snippet.length - 5);
      break;
    }
    case 'table':
      insertSnippet('\n| 欄位 1 | 欄位 2 |\n| --- | --- |\n| 內容 | 內容 |\n');
      break;
    case 'hr':
      insertSnippet('\n---\n');
      break;
    case 'link':
      insertLink();
      break;
    case 'image':
      insertImage();
      break;
    default:
      break;
  }
}

window.chrome?.webview?.addEventListener('message', (event) => {
  const data = event.data;
  if (!data || typeof data.type !== 'string') {
    return;
  }

  switch (data.type) {
    case 'setContent':
      setContent(data.text, data.documentId ?? null);
      break;
    case 'getContent':
      send({
        type: 'content',
        id: data.id,
        documentId: currentDocumentId,
        text: view.state.doc.toString(),
      });
      break;
    case 'command':
      applyCommand(data.name);
      break;
    case 'theme':
      setTheme(data.theme);
      break;
    case 'wordWrap':
      setWordWrap(data.enabled);
      break;
    case 'scroll':
      view.scrollDOM.scrollTop =
        (view.scrollDOM.scrollHeight - view.scrollDOM.clientHeight) * (data.percent ?? 0);
      break;
    case 'focus':
      view.focus();
      break;
    default:
      break;
  }
});

document.documentElement.dataset.theme = currentTheme;
send({ type: 'ready' });
