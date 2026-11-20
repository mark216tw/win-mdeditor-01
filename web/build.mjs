import { copyFile, mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import * as esbuild from 'esbuild';

const root = resolve(import.meta.dirname, '..');
const assets = resolve(root, 'src/MdEditor/Assets');

async function build() {
  const editorOutDir = resolve(assets, 'Editor');
  const previewOutDir = resolve(assets, 'Preview');
  await mkdir(editorOutDir, { recursive: true });
  await mkdir(previewOutDir, { recursive: true });
  await rm(resolve(editorOutDir, 'chunks'), { recursive: true, force: true });
  await rm(resolve(previewOutDir, 'chunks'), { recursive: true, force: true });

  await esbuild.build({
    entryPoints: {
      editor: resolve(root, 'web/editor/src/editor.js'),
    },
    bundle: true,
    splitting: true,
    format: 'esm',
    minify: true,
    sourcemap: false,
    target: ['chrome110'],
    outdir: editorOutDir,
    entryNames: '[name].bundle',
    chunkNames: 'chunks/[name]-[hash]',
  });

  await esbuild.build({
    entryPoints: {
      preview: resolve(root, 'web/preview/src/preview.js'),
    },
    bundle: true,
    splitting: true,
    format: 'esm',
    minify: true,
    sourcemap: false,
    target: ['chrome110'],
    outdir: previewOutDir,
    entryNames: '[name].bundle',
    chunkNames: 'chunks/[name]-[hash]',
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
  const katexCssPath = resolve(assets, 'Preview/katex.min.css');
  await copyFile(
    resolve(root, 'web/node_modules/katex/dist/katex.min.css'),
    katexCssPath);

  const katexCss = (await readFile(katexCssPath, 'utf8'))
    .replace(/,url\(fonts\/[^)]+\.woff\) format\("woff"\)/g, '')
    .replace(/,url\(fonts\/[^)]+\.ttf\) format\("truetype"\)/g, '');
  await writeFile(katexCssPath, katexCss);

  const katexPackage = JSON.parse(
    await readFile(resolve(root, 'web/node_modules/katex/package.json'), 'utf8'));
  await writeFile(
    resolve(assets, 'Preview/katex-version.txt'),
    `${katexPackage.version}\n`);

  const katexFonts = resolve(assets, 'Preview/fonts');
  await rm(katexFonts, { recursive: true, force: true });
  await mkdir(katexFonts, { recursive: true });

  const sourceFonts = resolve(root, 'web/node_modules/katex/dist/fonts');
  for (const file of await readdir(sourceFonts)) {
    if (file.endsWith('.woff2')) {
      await copyFile(resolve(sourceFonts, file), resolve(katexFonts, file));
    }
  }

  console.log('Web assets built.');
}

await build();
