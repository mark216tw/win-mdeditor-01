import { copyFile, cp, mkdir, rm } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import * as esbuild from 'esbuild';

const root = resolve(import.meta.dirname, '..');
const assets = resolve(root, 'src/MdEditor/Assets');

async function build() {
  await mkdir(resolve(assets, 'Editor'), { recursive: true });
  await mkdir(resolve(assets, 'Preview'), { recursive: true });

  await esbuild.build({
    entryPoints: [resolve(root, 'web/editor/src/editor.js')],
    bundle: true,
    format: 'iife',
    minify: true,
    sourcemap: false,
    target: ['chrome110'],
    outfile: resolve(assets, 'Editor/editor.bundle.js'),
  });

  await esbuild.build({
    entryPoints: [resolve(root, 'web/preview/src/preview.js')],
    bundle: true,
    format: 'iife',
    minify: true,
    sourcemap: false,
    target: ['chrome110'],
    outfile: resolve(assets, 'Preview/preview.bundle.js'),
  });

  await copyFile(
    resolve(root, 'web/node_modules/highlight.js/styles/github.min.css'),
    resolve(assets, 'Preview/github.min.css'));
  await copyFile(
    resolve(root, 'web/node_modules/highlight.js/styles/github-dark.min.css'),
    resolve(assets, 'Preview/github-dark.min.css'));

  await copyFile(
    resolve(root, 'web/node_modules/mermaid/dist/mermaid.min.js'),
    resolve(assets, 'Preview/mermaid.min.js'));
  await copyFile(
    resolve(root, 'web/node_modules/katex/dist/katex.min.css'),
    resolve(assets, 'Preview/katex.min.css'));

  const katexFonts = resolve(assets, 'Preview/fonts');
  await rm(katexFonts, { recursive: true, force: true });
  await cp(
    resolve(root, 'web/node_modules/katex/dist/fonts'),
    katexFonts,
    { recursive: true });

  console.log('Web assets built.');
}

await build();
