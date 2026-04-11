/**
 * sample-preview.js — Renders a single-frame WASM preview of a sample
 * into an xterm.js terminal embedded in the doc page.
 *
 * Usage: import from a <script type="module"> on each sample page.
 *   import { renderPreview } from '../sample-preview.js';
 *   renderPreview('progress-bars', 'preview-terminal');
 */

import { SAMPLES } from './samples.js';

let wasmMod = null;
let wasmLoading = null;

async function ensureWasm() {
  if (wasmMod) return wasmMod;
  if (wasmLoading) return wasmLoading;

  wasmLoading = (async () => {
    const { dotnet } = await import('./wasm/dotnet.js');
    const { getAssemblyExports, getConfig } = await dotnet
      .withApplicationArguments([])
      .create();
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);
    wasmMod = exports.TerminalNinja.Wasm.WasmModule;

    // Apply a default theme for nicer preview
    try { wasmMod.SetTheme('GruvboxDark'); } catch {}

    return wasmMod;
  })();

  return wasmLoading;
}

/**
 * Renders the sample identified by `sampleId` into the xterm.js terminal
 * mounted on the DOM element with the given `containerId`.
 */
export async function renderPreview(sampleId, containerId) {
  const container = document.getElementById(containerId);
  if (!container) return;

  const sample = SAMPLES.find(s => s.id === sampleId);
  if (!sample) {
    container.textContent = 'Sample not found: ' + sampleId;
    return;
  }

  // Show loading message
  const loadingEl = document.createElement('div');
  loadingEl.style.cssText = 'color:#95a7bf;font-family:"JetBrains Mono",monospace;font-size:0.82rem;padding:12px;';
  loadingEl.textContent = 'Loading preview\u2026';
  container.appendChild(loadingEl);

  try {
    const mod = await ensureWasm();

    // Remove loading message
    container.removeChild(loadingEl);

    const width = 80;
    const height = 24;

    const term = new Terminal({
      fontFamily: '"JetBrains Mono", "Consolas", "Courier New", monospace',
      fontSize: 13,
      lineHeight: 1.15,
      theme: {
        background: '#0a0e17',
        foreground: '#d8e2f1',
        cursor: '#3dd9b1',
        black: '#0a0e17',
        brightBlack: '#2a3950',
      },
      cursorStyle: 'block',
      cursorBlink: false,
      scrollback: 0,
      convertEol: true,
      disableStdin: true,
    });

    const fitAddon = new FitAddon.FitAddon();
    term.loadAddon(fitAddon);
    term.open(container);
    term.resize(width, height);
    fitAddon.fit();

    const ansi = mod.RenderXaml(sample.xaml, width, height);
    term.write('\x1b[2J\x1b[H');
    term.write(ansi);
  } catch (err) {
    container.removeChild(loadingEl);
    const errEl = document.createElement('div');
    errEl.style.cssText = 'color:#f87171;font-family:"JetBrains Mono",monospace;font-size:0.82rem;padding:12px;';
    errEl.textContent = 'Preview failed: ' + err.message;
    container.appendChild(errEl);
  }
}
