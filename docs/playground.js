/**
 * playground.js — Interactive XAML playground with live terminal rendering.
 *
 * Uses TerminalNinja's WASM module in "session" mode:
 *  - StartSession(xaml, w, h) creates a live Application with input backend
 *  - Tick() processes pending input events and returns ANSI delta
 *  - InjectKeyEvent / InjectMouseEvent forward xterm.js events
 *  - requestAnimationFrame drives the render loop (~60 FPS)
 *
 * Keyboard and mouse events from xterm.js are captured and forwarded to
 * the WASM input system, enabling hover states, button clicks, focus
 * changes, ListBox navigation, and ProgressBar animations.
 */

import { SAMPLES } from './samples.js';

// ── DOM refs ────────────────────────────────────────────────────────────────
const statusDot     = document.getElementById('status-dot');
const statusText    = document.getElementById('status-text');
const btnRender     = document.getElementById('btn-render');
const inputWidth    = document.getElementById('input-width');
const inputHeight   = document.getElementById('input-height');
const sampleSelect  = document.getElementById('sample-select');
const themeSelect   = document.getElementById('theme-select');
const autoRenderCb  = document.getElementById('auto-render');

function setStatus(state, text) {
  statusDot.className = `status-dot ${state}`;
  statusText.textContent = text;
}

// ── Populate sample selector ────────────────────────────────────────────────
SAMPLES.forEach(s => {
  const opt = document.createElement('option');
  opt.value = s.id;
  opt.textContent = s.title;
  sampleSelect.appendChild(opt);
});

// ── URL parameter handling ──────────────────────────────────────────────────
const params = new URLSearchParams(window.location.search);
const initialSampleId = params.get('sample') || SAMPLES[0].id;
const initialTheme    = params.get('theme')  || '';
sampleSelect.value = initialSampleId;

function getSelectedSample() {
  return SAMPLES.find(s => s.id === sampleSelect.value) || SAMPLES[0];
}

// ── xterm.js setup ──────────────────────────────────────────────────────────
const term = new Terminal({
  fontFamily: '"JetBrains Mono", "Consolas", "Courier New", monospace',
  fontSize: 14,
  lineHeight: 1.2,
  theme: {
    background: '#0a0e17',
    foreground: '#d8e2f1',
    cursor:     '#3dd9b1',
    black:      '#0a0e17',
    brightBlack:'#2a3950',
  },
  cursorStyle: 'block',
  cursorBlink: false,
  scrollback: 0,
  convertEol: true,
  allowProposedApi: true,
});

const fitAddon = new FitAddon.FitAddon();
term.loadAddon(fitAddon);
term.open(document.getElementById('xterm-container'));
fitAddon.fit();
term.write('\x1b[2J\x1b[H');
term.write('\x1b[38;2;149;167;191mLoading .NET WASM runtime\x1b[0m\r\n');
term.write('\x1b[38;2;61;217;177m\u25B6\x1b[0m This may take a few seconds on first load.\r\n');

window.addEventListener('resize', () => {
  fitAddon.fit();
  if (sessionActive && wasm.sessionResize) {
    wasm.sessionResize(term.cols, term.rows);
  }
});

// ── Monaco editor setup ─────────────────────────────────────────────────────
let monacoEditor = null;

require.config({
  paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs' },
});

require(['vs/editor/editor.main'], () => {
  monacoEditor = monaco.editor.create(
    document.getElementById('monaco-container'),
    {
      value:     getSelectedSample().xaml,
      language:  'xml',
      theme:     'vs-dark',
      fontSize:  13,
      lineHeight: 20,
      minimap:   { enabled: false },
      scrollBeyondLastLine: false,
      wordWrap:  'off',
      renderLineHighlight: 'all',
      fontFamily: '"JetBrains Mono", "Consolas", monospace',
    }
  );

  // Ctrl+Enter to start/restart session
  monacoEditor.addCommand(
    monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter,
    () => startLiveSession()
  );

  // Auto-render on content change (debounced restart)
  monacoEditor.onDidChangeModelContent(() => {
    if (autoRenderCb.checked && wasmReady) {
      scheduleAutoRender();
    }
  });
});

// ── Auto-render debounce ────────────────────────────────────────────────────
let autoRenderTimer = null;

function scheduleAutoRender() {
  clearTimeout(autoRenderTimer);
  autoRenderTimer = setTimeout(() => {
    if (sessionActive && wasm.reloadXaml) {
      // Live reload without restarting session
      liveReloadXaml();
    } else {
      startLiveSession();
    }
  }, 600);
}

function liveReloadXaml() {
  if (!wasmReady || !wasm.reloadXaml) return;

  const xaml = monacoEditor?.getValue() ?? '';
  fitAddon.fit();
  const width  = term.cols;
  const height = term.rows;

  try {
    const ansi = wasm.reloadXaml(xaml, width, height);
    term.write('\x1b[2J\x1b[H');
    if (ansi) term.write(ansi);
    setStatus('ready', 'Live \u2014 hot reloaded');
  } catch (err) {
    term.write(`\x1b[2J\x1b[H\x1b[31mReload error: ${err.message}\x1b[0m\r\n`);
  }
}

// ── Sample selector ─────────────────────────────────────────────────────────
sampleSelect.addEventListener('change', () => {
  const sample = getSelectedSample();
  if (!monacoEditor) return;
  monacoEditor.setValue(sample.xaml);
  if (wasmReady) startLiveSession();
});

// ── Theme selector ──────────────────────────────────────────────────────────
themeSelect.addEventListener('change', () => {
  if (!wasm.setTheme) return;
  try { wasm.setTheme(themeSelect.value || null); } catch {}
  if (wasmReady) startLiveSession();
});

// ── WASM module references ──────────────────────────────────────────────────
const wasm = {
  renderXaml: null,
  startSession: null,
  tick: null,
  injectKeyEvent: null,
  injectMouseEvent: null,
  sessionResize: null,
  stopSession: null,
  setTheme: null,
  getThemeNames: null,
};
let wasmReady = false;
let sessionActive = false;
let rafId = null;

// ── WASM bootstrap ─────────────────────────────────────────────────────────
async function loadWasm() {
  try {
    const { dotnet } = await import('./wasm/dotnet.js');

    const { getAssemblyExports, getConfig } = await dotnet
      .withApplicationArguments([])
      .create();

    const config  = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);
    const mod     = exports.TerminalNinja.Wasm.WasmModule;

    wasm.renderXaml      = (x, w, h) => mod.RenderXaml(x, w, h);
    wasm.startSession    = (x, w, h) => mod.StartSession(x, w, h);
    wasm.tick            = ()        => mod.Tick();
    wasm.injectKeyEvent  = (k, c, s, a, ct) => mod.InjectKeyEvent(k, c, s, a, ct);
    wasm.injectMouseEvent= (x, y, b, a) => mod.InjectMouseEvent(x, y, b, a);
    wasm.sessionResize   = (w, h)    => mod.SessionResize(w, h);
    wasm.stopSession     = ()        => mod.StopSession();
    wasm.reloadXaml      = (x, w, h) => mod.ReloadXaml(x, w, h);
    wasm.setTheme        = (n)       => mod.SetTheme(n);
    wasm.getThemeNames   = ()        => mod.GetThemeNames();

    // Populate theme dropdown
    try {
      const themes = wasm.getThemeNames();
      themes.forEach(name => {
        const opt = document.createElement('option');
        opt.value = name;
        opt.textContent = name;
        themeSelect.appendChild(opt);
      });
    } catch {
      ['Dark', 'Dracula', 'GruvboxDark'].forEach(name => {
        const opt = document.createElement('option');
        opt.value = name;
        opt.textContent = name;
        themeSelect.appendChild(opt);
      });
    }

    // Apply initial theme
    if (initialTheme) {
      themeSelect.value = initialTheme;
      try { wasm.setTheme(initialTheme); } catch {}
    }

    wasmReady = true;
    setStatus('ready', 'Ready \u2014 Ctrl+Enter to render');
    btnRender.disabled = false;

    term.write('\r\n\x1b[38;2;61;217;177m\u2713 Runtime loaded.\x1b[0m Press \x1b[1mRender\x1b[0m or \x1b[1mCtrl+Enter\x1b[0m to start a live session.\r\n');

    // Auto-start the initial sample
    startLiveSession();
  } catch (err) {
    setStatus('error', 'Runtime failed to load');
    term.write(`\r\n\x1b[31mFailed to load WASM runtime:\x1b[0m ${err.message}\r\n`);
    console.error(err);
  }
}

loadWasm();

// ── Live session management ─────────────────────────────────────────────────

function startLiveSession() {
  if (!wasmReady) return;

  // Stop any existing session and animation loop
  stopLiveSession();

  const xaml = monacoEditor?.getValue() ?? '';

  // Use xterm's fitted size as default, falling back to input fields
  fitAddon.fit();
  const width  = term.cols;
  const height = term.rows;

  try {
    // Apply current theme before session start
    const theme = themeSelect.value;
    if (theme) {
      try { wasm.setTheme(theme); } catch {}
    }

    const ansi = wasm.startSession(xaml, width, height);
    term.write('\x1b[2J\x1b[H');
    if (ansi) term.write(ansi);

    sessionActive = true;
    setStatus('ready', 'Live \u2014 interactive session');

    // Start the animation/tick loop
    tickLoop();
  } catch (err) {
    term.write(`\x1b[2J\x1b[H\x1b[31mSession error: ${err.message}\x1b[0m\r\n`);
    console.error(err);
  }
}

function stopLiveSession() {
  sessionActive = false;
  if (rafId !== null) {
    cancelAnimationFrame(rafId);
    rafId = null;
  }
  try { wasm.stopSession?.(); } catch {}
}

function tickLoop() {
  if (!sessionActive) return;

  try {
    const ansi = wasm.tick();
    if (ansi) {
      // Delta update — don't clear, just write the ANSI diff
      term.write(ansi);
    }
  } catch (err) {
    console.error('Tick error:', err);
  }

  rafId = requestAnimationFrame(tickLoop);
}

// ── Render button (starts/restarts live session) ────────────────────────────
btnRender.addEventListener('click', () => startLiveSession());

// ── Keyboard event forwarding from xterm.js ─────────────────────────────────

// Map xterm.js key names to ConsoleKey enum values
const KEY_MAP = {
  'Enter':      13,
  'Escape':     27,
  'Backspace':  8,
  'Tab':        9,
  'ArrowUp':    38,
  'ArrowDown':  40,
  'ArrowLeft':  37,
  'ArrowRight': 39,
  'Home':       36,
  'End':        35,
  'PageUp':     33,
  'PageDown':   34,
  'Delete':     46,
  'Insert':     45,
  'F1': 112, 'F2': 113, 'F3': 114, 'F4': 115,
  'F5': 116, 'F6': 117, 'F7': 118, 'F8': 119,
  'F9': 120, 'F10': 121, 'F11': 122, 'F12': 123,
  ' ':          32,
};

function domKeyToConsoleKey(domKey) {
  if (KEY_MAP[domKey] !== undefined) return KEY_MAP[domKey];
  // Single letter/digit: use the char code (A=65, 0=48, etc.)
  if (domKey.length === 1) {
    const code = domKey.toUpperCase().charCodeAt(0);
    if (code >= 65 && code <= 90) return code;   // A-Z
    if (code >= 48 && code <= 57) return code;   // 0-9
  }
  return 0; // Unknown
}

term.attachCustomKeyEventHandler((ev) => {
  if (!sessionActive || !wasm.injectKeyEvent) return true;

  // Only handle keydown (not keyup, keypress)
  if (ev.type !== 'keydown') return true;

  // Don't capture browser shortcuts (Ctrl+Shift+I, etc.)
  if (ev.ctrlKey && ev.shiftKey) return true;

  const consoleKey = domKeyToConsoleKey(ev.key);
  if (consoleKey === 0 && ev.key.length !== 1) return true; // Unrecognized special key

  const keyChar = ev.key.length === 1 ? ev.key.charCodeAt(0) : 0;

  try {
    wasm.injectKeyEvent(consoleKey, keyChar, ev.shiftKey, ev.altKey, ev.ctrlKey);
  } catch (err) {
    console.error('InjectKeyEvent error:', err);
  }

  // Prevent xterm from processing the key itself
  return false;
});

// ── Mouse event forwarding from xterm.js ────────────────────────────────────

// Mouse button mapping: DOM uses 0=left, 1=middle, 2=right
// TerminalNinja enum: None=0, Left=1, Middle=2, Right=3
// Mouse action mapping: Press=0, Release=1, Move=2

const xtermContainer = document.getElementById('xterm-container');

function getTerminalCellCoords(ev) {
  // Get the xterm.js viewport element
  const viewport = xtermContainer.querySelector('.xterm-screen');
  if (!viewport) return null;

  const rect = viewport.getBoundingClientRect();
  const cellWidth  = rect.width  / term.cols;
  const cellHeight = rect.height / term.rows;

  const x = Math.floor((ev.clientX - rect.left) / cellWidth);
  const y = Math.floor((ev.clientY - rect.top)  / cellHeight);

  if (x < 0 || y < 0 || x >= term.cols || y >= term.rows) return null;
  return { x, y };
}

function domButtonToEnum(domButton) {
  // DOM: 0=left, 1=middle, 2=right → TerminalNinja: None=0, Left=1, Middle=2, Right=3
  return domButton + 1;
}

xtermContainer.addEventListener('mousedown', (ev) => {
  if (!sessionActive || !wasm.injectMouseEvent) return;
  const coords = getTerminalCellCoords(ev);
  if (!coords) return;
  wasm.injectMouseEvent(coords.x, coords.y, domButtonToEnum(ev.button), 0); // Press
});

xtermContainer.addEventListener('mouseup', (ev) => {
  if (!sessionActive || !wasm.injectMouseEvent) return;
  const coords = getTerminalCellCoords(ev);
  if (!coords) return;
  wasm.injectMouseEvent(coords.x, coords.y, domButtonToEnum(ev.button), 1); // Release
});

xtermContainer.addEventListener('mousemove', (ev) => {
  if (!sessionActive || !wasm.injectMouseEvent) return;
  const coords = getTerminalCellCoords(ev);
  if (!coords) return;
  wasm.injectMouseEvent(coords.x, coords.y, 0, 2); // Move (None button)
});

xtermContainer.addEventListener('wheel', (ev) => {
  if (!sessionActive || !wasm.injectMouseEvent) return;
  const coords = getTerminalCellCoords(ev);
  if (!coords) return;
  // ScrollUp=3, ScrollDown=4 (MouseAction enum)
  const action = ev.deltaY < 0 ? 3 : 4;
  wasm.injectMouseEvent(coords.x, coords.y, 0, action);
  ev.preventDefault();
}, { passive: false });
