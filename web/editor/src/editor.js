import { basicSetup } from 'codemirror';
import { EditorView, keymap } from '@codemirror/view';
import { EditorState, Compartment } from '@codemirror/state';
import { indentWithTab, redo, selectAll, undo } from '@codemirror/commands';
import { openSearchPanel } from '@codemirror/search';
import { markdown, markdownLanguage } from '@codemirror/lang-markdown';
import { syntaxHighlighting } from '@codemirror/language';
import { languages } from './languages.js';
import { oneDarkHighlightStyle } from '@codemirror/theme-one-dark';

const themeCompartment = new Compartment();
const wrapCompartment = new Compartment();
const readOnlyCompartment = new Compartment();

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
const phrases = {
  'Find': '尋找',
  'Replace': '取代',
  'next': '下一個',
  'previous': '上一個',
  'all': '全部選取',
  'match case': '大小寫相符',
  'regexp': '規則運算式',
  'by word': '全字相符',
  'replace': '取代',
  'replace all': '全部取代',
  'close': '關閉',
  'current match': '目前相符項目',
  'on line': '位於第',
  'Go to line': '前往行',
  'go': '前往',
  'replaced $ matches': '已取代 $ 處',
  'replaced match on line $': '已取代第 $ 行的相符內容',
};

function normalizeTheme(theme) {
  return knownThemes.has(theme) ? theme : 'light';
}

function themeExtensions(theme) {
  return darkThemes.has(theme) ? darkThemeExtensions : lightThemeExtensions;
}

let currentTheme = 'light';
let currentWordWrap = true;
let currentReadOnly = false;
let currentFontFamily = 'Cascadia Mono';
let currentFontSize = 14;
let includeTextInChange = true;
let currentDocumentId = null;
let suppressChange = false;
let changeTimer = null;
let selectionTimer = null;
let scrollRaf = 0;
let wheelAccumulator = 0;
const sessionStates = new Map();

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
        key: 'Mod-Shift-t',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'reopenClosed' });
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
        key: 'Mod-k',
        preventDefault: true,
        run: () => {
          applyCommand('link');
          return true;
        },
      },
      {
        key: 'Mod-h',
        preventDefault: true,
        run: () => {
          applyCommand('replace');
          return true;
        },
      },
      {
        key: 'Ctrl-Tab',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'nextTab' });
          return true;
        },
      },
      {
        key: 'Ctrl-Shift-Tab',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'previousTab' });
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
      {
        key: 'Mod-=',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'zoomIn' });
          return true;
        },
      },
      {
        key: 'Mod-+',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'zoomIn' });
          return true;
        },
      },
      {
        key: 'Mod--',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'zoomOut' });
          return true;
        },
      },
      {
        key: 'Mod-0',
        preventDefault: true,
        run: () => {
          send({ type: 'shortcut', name: 'zoomReset' });
          return true;
        },
      },
    ]),
    markdown({ base: markdownLanguage, codeLanguages: languages }),
    EditorState.tabSize.of(4),
    EditorState.phrases.of(phrases),
    themeCompartment.of(themeExtensions(currentTheme)),
    wrapCompartment.of(currentWordWrap ? EditorView.lineWrapping : []),
    readOnlyCompartment.of([
      EditorState.readOnly.of(currentReadOnly),
      EditorView.editable.of(!currentReadOnly),
    ]),
    EditorView.domEventHandlers({
      paste(event, editorView) {
        if (currentReadOnly) {
          return false;
        }

        const items = Array.from(event.clipboardData?.items ?? []);
        const imageItem = items.find((item) => item.kind === 'file' && item.type.startsWith('image/'));
        if (imageItem) {
          const file = imageItem.getAsFile();
          if (file) {
            event.preventDefault();
            const targetDocumentId = currentDocumentId;
            const mimeType = file.type || imageItem.type;
            if (file.size > 20 * 1024 * 1024) {
              send({
                type: 'pasteImage',
                documentId: targetDocumentId,
                mimeType,
                base64: '',
                tooLarge: true,
              });
              return true;
            }

            const reader = new FileReader();
            reader.onload = () => {
              if (typeof reader.result !== 'string') {
                return;
              }

              const comma = reader.result.indexOf(',');
              const base64 = comma >= 0 ? reader.result.slice(comma + 1) : reader.result;
              send({
                type: 'pasteImage',
                documentId: targetDocumentId,
                mimeType,
                base64,
              });
            };
            reader.readAsDataURL(file);
            return true;
          }
        }

        const { from, to } = editorView.state.selection.main;
        if (from === to) {
          return false;
        }

        const selected = editorView.state.sliceDoc(from, to);
        if (selected.includes('\n')) {
          return false;
        }

        const pasted = event.clipboardData?.getData('text/plain')?.trim() ?? '';
        if (!/^https?:\/\/\S+$/i.test(pasted)) {
          return false;
        }

        event.preventDefault();
        const insert = `[${selected}](${pasted})`;
        editorView.dispatch({
          changes: { from, to, insert },
          selection: { anchor: from + insert.length },
        });
        return true;
      },
    }),
    EditorView.updateListener.of((update) => {
      if (suppressChange) {
        return;
      }

      if (update.docChanged) {
        clearTimeout(changeTimer);
        changeTimer = setTimeout(() => {
          const message = {
            type: 'change',
            documentId: currentDocumentId,
          };

          if (includeTextInChange) {
            message.text = view.state.doc.toString();
          }

          send(message);
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

function getTopVisibleLine() {
  const scroller = view.scrollDOM;
  if (scroller.scrollTop <= 0) {
    return 1;
  }

  const docOffset = Math.max(0, scroller.getBoundingClientRect().top - view.documentTop);
  const block = view.lineBlockAtHeight(docOffset);
  const line = view.state.doc.lineAt(block.from);
  const fraction = block.height > 0
    ? Math.max(0, Math.min(1, (docOffset - block.top) / block.height))
    : 0;
  return line.number + fraction;
}

function sendScroll() {
  const scroller = view.scrollDOM;
  const max = scroller.scrollHeight - scroller.clientHeight;
  send({
    type: 'scroll',
    documentId: currentDocumentId,
    percent: max > 0 ? scroller.scrollTop / max : 0,
    line: getTopVisibleLine(),
  });
}

view.scrollDOM.addEventListener('scroll', () => {
  if (scrollRaf) {
    return;
  }

  scrollRaf = requestAnimationFrame(() => {
    scrollRaf = 0;
    sendScroll();
  });
}, { passive: true });

view.scrollDOM.addEventListener('wheel', (event) => {
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
  const next = Math.min(28, Math.max(10, currentFontSize + direction));
  if (next === currentFontSize) {
    return;
  }

  setFontSize(next);
  send({ type: 'fontSize', size: next });
}, { passive: false });

function setContent(text, documentId) {
  if (currentDocumentId) {
    sessionStates.set(currentDocumentId, {
      state: view.state,
      scrollTop: view.scrollDOM.scrollTop,
    });
  }

  currentDocumentId = documentId;
  suppressChange = true;
  clearTimeout(changeTimer);

  const incoming = text ?? '';
  const cached = documentId ? sessionStates.get(documentId) : null;

  if (cached && cached.state.doc.toString() === incoming) {
    view.setState(cached.state);
    view.dispatch({
      effects: [
        themeCompartment.reconfigure(themeExtensions(currentTheme)),
        wrapCompartment.reconfigure(currentWordWrap ? EditorView.lineWrapping : []),
        readOnlyCompartment.reconfigure([
          EditorState.readOnly.of(currentReadOnly),
          EditorView.editable.of(!currentReadOnly),
        ]),
      ],
    });
    view.scrollDOM.scrollTop = cached.scrollTop;
    requestAnimationFrame(() => {
      if (currentDocumentId === documentId) {
        view.scrollDOM.scrollTop = cached.scrollTop;
        sendScroll();
      }
    });
  } else {
    if (documentId) {
      sessionStates.delete(documentId);
    }

    view.setState(EditorState.create({
      doc: incoming,
      extensions: buildExtensions(),
    }));
    view.scrollDOM.scrollTop = 0;
  }

  suppressChange = false;
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

function setReadOnly(enabled) {
  currentReadOnly = Boolean(enabled);
  view.dispatch({
    effects: readOnlyCompartment.reconfigure([
      EditorState.readOnly.of(currentReadOnly),
      EditorView.editable.of(!currentReadOnly),
    ]),
  });
}

function setFontSize(size) {
  const value = Number(size);
  if (!Number.isFinite(value)) {
    return;
  }

  currentFontSize = Math.min(28, Math.max(10, value));
  document.documentElement.style.setProperty(
    '--editor-font-size',
    `${currentFontSize}px`);
}

function setFontFamily(family) {
  const value = String(family ?? '').replace(/[";\\]/g, '').trim() || 'Cascadia Mono';
  currentFontFamily = value;
  document.documentElement.style.setProperty(
    '--editor-font-family',
    `"${value}", "Cascadia Mono", Consolas, monospace`);
}

function currentSelection() {
  const selection = view.state.selection.main;
  return {
    selection,
    text: view.state.sliceDoc(selection.from, selection.to),
  };
}

function isAsteriskFalseMatch(beforeText, afterText, marker) {
  if (marker !== '*') {
    return false;
  }

  return beforeText.endsWith('**') &&
         afterText.startsWith('**') &&
         !(beforeText.endsWith('***') && afterText.startsWith('***'));
}

function wrapInline(before, after, placeholder) {
  const { selection, text } = currentSelection();
  const bLen = before.length;
  const aLen = after.length;

  const contextBefore = view.state.sliceDoc(Math.max(0, selection.from - 3), selection.from);
  const contextAfter = view.state.sliceDoc(selection.to, selection.to + 3);
  const outerBefore = view.state.sliceDoc(Math.max(0, selection.from - bLen), selection.from);
  const outerAfter = view.state.sliceDoc(selection.to, selection.to + aLen);

  if (selection.from >= bLen &&
      outerBefore === before &&
      outerAfter === after &&
      !isAsteriskFalseMatch(contextBefore, contextAfter, before)) {
    view.dispatch({
      changes: [
        { from: selection.from - bLen, to: selection.from, insert: '' },
        { from: selection.to, to: selection.to + aLen, insert: '' },
      ],
      selection: {
        anchor: selection.from - bLen,
        head: selection.to - bLen,
      },
    });
    view.focus();
    return;
  }

  if (text.length >= bLen + aLen &&
      text.startsWith(before) &&
      text.endsWith(after) &&
      !(before === '*' &&
        text.startsWith('**') &&
        text.endsWith('**') &&
        !(text.startsWith('***') && text.endsWith('***')))) {
    const unwrapped = text.slice(bLen, text.length - aLen);
    view.dispatch({
      changes: { from: selection.from, to: selection.to, insert: unwrapped },
      selection: {
        anchor: selection.from,
        head: selection.from + unwrapped.length,
      },
    });
    view.focus();
    return;
  }

  const inner = text || placeholder;
  const insert = before + inner + after;
  view.dispatch({
    changes: { from: selection.from, to: selection.to, insert },
    selection: {
      anchor: selection.from + bLen,
      head: selection.from + bLen + inner.length,
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
  if (currentReadOnly) {
    return;
  }

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
    case 'undo':
      undo(view);
      view.focus();
      break;
    case 'redo':
      redo(view);
      view.focus();
      break;
    case 'selectAll':
      selectAll(view);
      view.focus();
      break;
    case 'find':
      openSearchPanel(view);
      break;
    case 'replace':
      openSearchPanel(view);
      view.dom.querySelector('.cm-textfield[name="replace"]')?.focus();
      break;
    default:
      break;
  }
}

function toggleTaskAtLine(documentId, lineNumber, checked) {
  if (currentReadOnly || (documentId && documentId !== currentDocumentId)) {
    return;
  }

  if (!Number.isInteger(lineNumber) || lineNumber < 1 || lineNumber > view.state.doc.lines) {
    return;
  }

  const line = view.state.doc.line(lineNumber);
  const match = line.text.match(/^(\s*(?:>\s*)*(?:[-*+]|\d+[.)])\s+\[)[ xX](\])/);
  if (!match) {
    return;
  }

  const bracketCharPos = line.from + match[1].length;
  const replacement = checked ? 'x' : ' ';
  if (view.state.sliceDoc(bracketCharPos, bracketCharPos + 1) === replacement) {
    return;
  }

  view.dispatch({
    changes: { from: bracketCharPos, to: bracketCharPos + 1, insert: replacement },
  });
}

window.chrome?.webview?.addEventListener('message', (event) => {
  const data = event.data;
  if (!data || typeof data.type !== 'string') {
    return;
  }

  switch (data.type) {
    case 'setContent':
      setContent(data.text, data.documentId ?? null);
      send({ type: 'contentSet', id: data.id });
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
    case 'fontFamily':
      setFontFamily(data.family);
      break;
    case 'font':
      setFontSize(data.size);
      break;
    case 'changeMode':
      includeTextInChange = Boolean(data.includeText);
      break;
    case 'readOnly':
      setReadOnly(data.enabled);
      break;
    case 'disposeDocument':
      if (data.documentId) {
        sessionStates.delete(data.documentId);
      }
      break;
    case 'insertText':
      if (!currentReadOnly && (!data.documentId || data.documentId === currentDocumentId)) {
        insertSnippet(data.text ?? '', 0);
      }
      break;
    case 'toggleTask':
      toggleTaskAtLine(data.documentId, Number(data.line), Boolean(data.checked));
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
