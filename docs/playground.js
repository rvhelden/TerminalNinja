/**
 * playground.js — wires up Monaco editor + xterm.js + TerminalNinja WASM module.
 *
 * Loading sequence:
 *  1. xterm.js terminal initialises immediately (shows "Loading runtime..." message).
 *  2. Monaco editor loads via AMD require().
 *  3. .NET browser-WASM runtime bootstraps in the background.
 *  4. Once the WASM export is ready the Render button is enabled.
 *  5. On Render: XAML string is passed to WasmModule.RenderXaml(), output written to xterm.
 */

// ── Default XAML snippet shown in the editor on first load ──────────────────
const DEFAULT_XAML = `<Window xmlns="http://schemas.terminalninja.dev/xaml"
        Title="Playground"
        Width="80"
        Height="24">
  <StackPanel Orientation="Vertical">

    <Border BorderStyle="Rounded"
            Background="#1E1E50"
            Foreground="#3DD9B1">
      <TextBlock Text="  TerminalNinja Playground  "
                 HorizontalTextAlignment="Center" />
    </Border>

    <TextBlock Text=""
               Foreground="#95A7BF" />

    <TextBlock Text="  Edit this XAML and click Render!"
               Foreground="#D8E2F1" />

    <TextBlock Text=""
               Foreground="#95A7BF" />

    <Button Text="Hello World" />

    <TextBlock Text=""
               Foreground="#95A7BF" />

    <TextBlock Text="  StackPanel · Grid · Border · TextBlock · Button"
               Foreground="#49A7FF" />

  </StackPanel>
</Window>`;

// ── DOM refs ─────────────────────────────────────────────────────────────────
const statusDot  = document.getElementById('status-dot');
const statusText = document.getElementById('status-text');
const btnRender  = document.getElementById('btn-render');
const inputWidth  = document.getElementById('input-width');
const inputHeight = document.getElementById('input-height');

function setStatus(state, text) {
  statusDot.className = `status-dot ${state}`;
  statusText.textContent = text;
}

// ── xterm.js setup ───────────────────────────────────────────────────────────
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
});

const fitAddon = new FitAddon.FitAddon();
term.loadAddon(fitAddon);
term.open(document.getElementById('xterm-container'));
fitAddon.fit();
term.write('\x1b[2J\x1b[H'); // clear
term.write('\x1b[38;2;149;167;191mLoading .NET WASM runtime\x1b[0m\r\n');
term.write('\x1b[38;2;61;217;177m▶\x1b[0m This may take a few seconds on first load.\r\n');

window.addEventListener('resize', () => fitAddon.fit());

// ── Monaco editor setup ──────────────────────────────────────────────────────
let monacoEditor = null;

require.config({
  paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs' },
});

require(['vs/editor/editor.main'], () => {
  monacoEditor = monaco.editor.create(
    document.getElementById('monaco-container'),
    {
      value:     DEFAULT_XAML,
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

  // Render on Ctrl+Enter / Cmd+Enter
  monacoEditor.addCommand(
    monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter,
    () => btnRender.click()
  );
});

// ── .NET WASM bootstrap ──────────────────────────────────────────────────────
let renderXaml = null; // set once WASM is ready

async function loadWasm() {
  try {
    const { dotnet } = await import('./wasm/dotnet.js');

    const { getAssemblyExports, getConfig } = await dotnet
      .withApplicationArguments([])
      .create();

    const config   = getConfig();
    const exports  = await getAssemblyExports(config.mainAssemblyName);

    renderXaml = (xaml, width, height) =>
      exports.TerminalNinja.Wasm.WasmModule.RenderXaml(xaml, width, height);

    setStatus('ready', 'Ready — Ctrl+Enter to render');
    btnRender.disabled = false;

    term.write('\r\n\x1b[38;2;61;217;177m✓ Runtime loaded.\x1b[0m Press \x1b[1mRender\x1b[0m or \x1b[1mCtrl+Enter\x1b[0m to preview your XAML.\r\n');
  } catch (err) {
    setStatus('error', 'Runtime failed to load');
    term.write(`\r\n\x1b[31mFailed to load WASM runtime:\x1b[0m ${err.message}\r\n`);
    console.error(err);
  }
}

loadWasm();

// ── Render button ─────────────────────────────────────────────────────────────
btnRender.addEventListener('click', () => {
  if (!renderXaml) return;

  const xaml   = monacoEditor?.getValue() ?? '';
  const width  = Math.max(20, parseInt(inputWidth.value,  10) || 80);
  const height = Math.max(5,  parseInt(inputHeight.value, 10) || 24);

  // Resize terminal to match requested dimensions before writing
  term.resize(width, height);

  try {
    const ansi = renderXaml(xaml, width, height);
    term.write('\x1b[2J\x1b[H'); // clear screen + home
    term.write(ansi);
  } catch (err) {
    term.write(`\x1b[2J\x1b[H\x1b[31mRender error: ${err.message}\x1b[0m\r\n`);
    console.error(err);
  }
});
