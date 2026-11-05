import { LanguageDescription, LanguageSupport, StreamLanguage } from '@codemirror/language';
import { javascript } from '@codemirror/lang-javascript';
import { html } from '@codemirror/lang-html';
import { css } from '@codemirror/lang-css';
import { json } from '@codemirror/lang-json';
import { python } from '@codemirror/lang-python';
import { java } from '@codemirror/lang-java';
import { cpp } from '@codemirror/lang-cpp';
import { sql } from '@codemirror/lang-sql';
import { xml } from '@codemirror/lang-xml';
import { yaml } from '@codemirror/lang-yaml';
import { csharp } from '@codemirror/legacy-modes/mode/clike';
import { shell } from '@codemirror/legacy-modes/mode/shell';

function legacy(parser) {
  return new LanguageSupport(StreamLanguage.define(parser));
}

export const languages = [
  LanguageDescription.of({
    name: 'JavaScript',
    alias: ['ecmascript', 'js', 'node'],
    extensions: ['js', 'mjs', 'cjs'],
    support: javascript(),
  }),
  LanguageDescription.of({
    name: 'TypeScript',
    alias: ['ts'],
    extensions: ['ts', 'mts', 'cts'],
    support: javascript({ typescript: true }),
  }),
  LanguageDescription.of({
    name: 'JSX',
    extensions: ['jsx'],
    support: javascript({ jsx: true }),
  }),
  LanguageDescription.of({
    name: 'TSX',
    extensions: ['tsx'],
    support: javascript({ jsx: true, typescript: true }),
  }),
  LanguageDescription.of({
    name: 'HTML',
    alias: ['xhtml'],
    extensions: ['html', 'htm', 'handlebars', 'hbs'],
    support: html(),
  }),
  LanguageDescription.of({
    name: 'CSS',
    extensions: ['css'],
    support: css(),
  }),
  LanguageDescription.of({
    name: 'JSON',
    alias: ['json5'],
    extensions: ['json', 'map'],
    support: json(),
  }),
  LanguageDescription.of({
    name: 'Python',
    extensions: ['BUILD', 'bzl', 'py', 'pyw'],
    filename: /^(BUCK|BUILD)$/,
    support: python(),
  }),
  LanguageDescription.of({
    name: 'Java',
    extensions: ['java'],
    support: java(),
  }),
  LanguageDescription.of({
    name: 'C++',
    alias: ['cpp'],
    extensions: ['cpp', 'c++', 'cc', 'cxx', 'hpp', 'h++', 'hh', 'hxx'],
    support: cpp(),
  }),
  LanguageDescription.of({
    name: 'C#',
    alias: ['csharp', 'cs'],
    extensions: ['cs'],
    support: legacy(csharp),
  }),
  LanguageDescription.of({
    name: 'Shell',
    alias: ['bash', 'sh', 'zsh'],
    extensions: ['sh', 'ksh', 'bash'],
    filename: /^PKGBUILD$/,
    support: legacy(shell),
  }),
  LanguageDescription.of({
    name: 'SQL',
    extensions: ['sql'],
    support: sql(),
  }),
  LanguageDescription.of({
    name: 'XML',
    alias: ['rss', 'wsdl', 'xsd'],
    extensions: ['xml', 'xsl', 'xsd', 'svg'],
    support: xml(),
  }),
  LanguageDescription.of({
    name: 'YAML',
    alias: ['yml'],
    extensions: ['yaml', 'yml'],
    support: yaml(),
  }),
];
