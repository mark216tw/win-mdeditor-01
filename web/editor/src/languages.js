import { LanguageDescription, LanguageSupport, StreamLanguage } from '@codemirror/language';

function legacy(loader) {
  return async () => new LanguageSupport(StreamLanguage.define(await loader()));
}

export const languages = [
  LanguageDescription.of({
    name: 'JavaScript',
    alias: ['ecmascript', 'js', 'node'],
    extensions: ['js', 'mjs', 'cjs'],
    load: () => import('@codemirror/lang-javascript').then((m) => m.javascript()),
  }),
  LanguageDescription.of({
    name: 'TypeScript',
    alias: ['ts'],
    extensions: ['ts', 'mts', 'cts'],
    load: () => import('@codemirror/lang-javascript').then((m) => m.javascript({ typescript: true })),
  }),
  LanguageDescription.of({
    name: 'JSX',
    extensions: ['jsx'],
    load: () => import('@codemirror/lang-javascript').then((m) => m.javascript({ jsx: true })),
  }),
  LanguageDescription.of({
    name: 'TSX',
    extensions: ['tsx'],
    load: () => import('@codemirror/lang-javascript').then((m) => m.javascript({ jsx: true, typescript: true })),
  }),
  LanguageDescription.of({
    name: 'HTML',
    alias: ['xhtml'],
    extensions: ['html', 'htm', 'handlebars', 'hbs'],
    load: () => import('@codemirror/lang-html').then((m) => m.html()),
  }),
  LanguageDescription.of({
    name: 'CSS',
    extensions: ['css'],
    load: () => import('@codemirror/lang-css').then((m) => m.css()),
  }),
  LanguageDescription.of({
    name: 'JSON',
    alias: ['json5'],
    extensions: ['json', 'map'],
    load: () => import('@codemirror/lang-json').then((m) => m.json()),
  }),
  LanguageDescription.of({
    name: 'Python',
    extensions: ['BUILD', 'bzl', 'py', 'pyw'],
    filename: /^(BUCK|BUILD)$/,
    load: () => import('@codemirror/lang-python').then((m) => m.python()),
  }),
  LanguageDescription.of({
    name: 'Java',
    extensions: ['java'],
    load: () => import('@codemirror/lang-java').then((m) => m.java()),
  }),
  LanguageDescription.of({
    name: 'C++',
    alias: ['cpp'],
    extensions: ['cpp', 'c++', 'cc', 'cxx', 'hpp', 'h++', 'hh', 'hxx'],
    load: () => import('@codemirror/lang-cpp').then((m) => m.cpp()),
  }),
  LanguageDescription.of({
    name: 'C#',
    alias: ['csharp', 'cs'],
    extensions: ['cs'],
    load: legacy(() => import('@codemirror/legacy-modes/mode/clike').then((m) => m.csharp)),
  }),
  LanguageDescription.of({
    name: 'Shell',
    alias: ['bash', 'sh', 'zsh'],
    extensions: ['sh', 'ksh', 'bash'],
    filename: /^PKGBUILD$/,
    load: legacy(() => import('@codemirror/legacy-modes/mode/shell').then((m) => m.shell)),
  }),
  LanguageDescription.of({
    name: 'SQL',
    extensions: ['sql'],
    load: () => import('@codemirror/lang-sql').then((m) => m.sql()),
  }),
  LanguageDescription.of({
    name: 'XML',
    alias: ['rss', 'wsdl', 'xsd'],
    extensions: ['xml', 'xsl', 'xsd', 'svg'],
    load: () => import('@codemirror/lang-xml').then((m) => m.xml()),
  }),
  LanguageDescription.of({
    name: 'YAML',
    alias: ['yml'],
    extensions: ['yaml', 'yml'],
    load: () => import('@codemirror/lang-yaml').then((m) => m.yaml()),
  }),
];
