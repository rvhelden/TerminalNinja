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
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       x:Name="App"
       xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
       xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
       xmlns:data="clr-namespace:TerminalNinja.Xaml.Data;assembly=TerminalNinja"
       mc:Ignorable="d"
       d:DataContext="{d:DesignInstance sample:DemoViewModel}"
       Title="TerminalNinja Demo">

    <!-- Window-level Resources -->
    <Window.Resources>
        <Color x:Key="HeaderBackground">#1E1E50</Color>
        <Color x:Key="ToolbarBackground">#19192D</Color>
        <Color x:Key="ContentBackground">#FF0000</Color>
        <Color x:Key="StatusBackground">#282828</Color>
        <Color x:Key="ButtonBackground">#141414</Color>
        <Color x:Key="AccentColor">Cyan</Color>
        <Color x:Key="HighlightColor">Yellow</Color>
        <data:DateTimeToStringConverter x:Key="DateTimeToStringConverter" />
    </Window.Resources>

    <!-- Rows: Header=5, Toolbar=5, Content=*, StatusBar=3 -->
    <!-- Columns: Content=*, ActivityLog=35, Menu=24 -->
    <Grid Rows="5 5 * 3" Columns="* 35 24">

        <!-- Header spanning all 3 columns -->
        <Border Grid.Row="0" Grid.ColumnSpan="3"
                   Background="{Binding BackgroundColor}"
                   Foreground="{StaticResource AccentColor}"
                   BorderStyle="Double">
            <TextBlock x:Name="HeaderTextBlock"
                   Text="{Binding HeaderText}"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center" />
        </Border>

        <!-- Toolbar spanning all 3 columns -->
        <Border Grid.Row="1" Grid.ColumnSpan="3"
                   Background="{StaticResource ToolbarBackground}"
                   BorderStyle="Single">
            <StackPanel Orientation="Horizontal">
                <Border StackPanel.SizeMode="Auto"
                           Width="2"
                           Background="{StaticResource ToolbarBackground}" />

                <Button x:Name="NewButton"
                        StackPanel.SizeMode="Auto"
                        Text="New"
                        Width="12"
                        Height="3"
                        TabIndex="0"
                        FocusColor="{StaticResource AccentColor}"
                        HoverColor="{StaticResource HighlightColor}"
                        Foreground="White"
                        Background="{StaticResource ButtonBackground}"
                        Command="{Binding NewCommand}" />

                <Button x:Name="GcCollectButton" Text="GC Collect" Command="{Binding GCCollect}" />
                
                <Button x:Name="OpenButton"
                        StackPanel.SizeMode="Auto"
                        Text="Open"
                        Width="12"
                        Height="3"
                        TabIndex="1"
                        FocusColor="{StaticResource AccentColor}"
                        HoverColor="{StaticResource HighlightColor}"
                        Foreground="White"
                        Background="{StaticResource ButtonBackground}"
                        Command="{Binding Path=OpenCommand}" />

                <Button x:Name="SaveButton"
                        StackPanel.SizeMode="Auto"
                        Text="Save"
                        Width="12"
                        Height="3"
                        TabIndex="2"
                        FocusColor="{StaticResource AccentColor}"
                        HoverColor="{StaticResource HighlightColor}"
                        Foreground="White"
                        Background="{StaticResource ButtonBackground}"
                        Command="{Binding Path=SaveCommand}" />

                <Border StackPanel.SizeMode="Stretch" Background="{StaticResource ToolbarBackground}" />
            </StackPanel>
        </Border>

        <!-- Content area: column 0, row 2 -->
        <Border Grid.Row="2" Grid.Column="0"
                   Background="{StaticResource ContentBackground}"
                   BorderStyle="Rounded">
            <TextBlock x:Name="ContentTextBlock"
                   Text="{Binding Path=ContentText}"
                   Foreground="White"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   TextWrapping="Wrap"
                   Padding="4,2,4,2" />
        </Border>

        <!-- Menu: column 2, row 2 -->
        <Border Grid.Row="2" Grid.Column="2" Grid.ColumnSpan="2"
                   Background="#1A1A2E"
                   Foreground="White"
                   BorderStyle="Rounded">
            <StackPanel Orientation="Vertical">
                <TextBlock StackPanel.SizeMode="Fixed" StackPanel.FixedSize="1"
                       Text=" Menu"
                       Foreground="{StaticResource AccentColor}"
                       Background="#1A1A2E" />
                <ListBox StackPanel.SizeMode="Stretch"
                         x:Name="MenuListBox"
                         Background="#1A1A2E"
                         Foreground="White"
                         SelectedBackground="#2D5AA0"
                         SelectedForeground="White"
                         TabIndex="3">                         
                <ListBoxItem>ListBox Item #1</ListBoxItem>
                <ListBoxItem>ListBox Item #2</ListBoxItem>
                <ListBoxItem>ListBox Item #3</ListBoxItem>
                </ListBox>
            </StackPanel>
        </Border>

        <!-- Status Bar spanning all 3 columns -->
        <Border Grid.Row="3" Grid.ColumnSpan="3"
                   Background="{StaticResource StatusBackground}"
                   Foreground="{StaticResource HighlightColor}"
                   BorderStyle="Single">
            <StackPanel Orientation="Horizontal">

            <TextBlock StackPanel.SizeMode="Stretch" x:Name="StatusTextBlock"
                   Text="{Binding Path=StatusText}"
                   Foreground="{StaticResource HighlightColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="MemoryTextBlock"
                   Text="{Binding MemoryUsageMB, Converter={StaticResource PerformanceConverter}, ConverterParameter=Memory}"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="2,0,2,0" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="CpuTextBlock"
                   Text="{Binding CpuUsagePercent, Converter={StaticResource PerformanceConverter}, ConverterParameter=Cpu}"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="2,0,2,0" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="FpsLabelTextBlock"
                   Text="FPS:"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="2,0,0,0" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="CurrentFpsTextBlock"
                   Text="{Binding CurrentFps, Converter={StaticResource PerformanceConverter}, ConverterParameter=CurrentFps}"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="0,0,0,0" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="FpsSeparatorTextBlock"
                   Text="/"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="0,0,0,0" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="TargetFpsTextBlock"
                   Text="{Binding TargetFps, Converter={StaticResource PerformanceConverter}, ConverterParameter=TargetFps}"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="0,0,2,0" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="TtfrTextBlock"
                   Text="{Binding TimeToFirstRenderMs, Converter={StaticResource PerformanceConverter}, ConverterParameter=TTFR}"
                   Foreground="{StaticResource AccentColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="2,0,2,0" />

            <TextBlock StackPanel.SizeMode="Auto" x:Name="TimeTextBlock"
                   Text="{Binding CurrentTime, Converter={StaticResource DateTimeToStringConverter}, ConverterParameter=HH:mm:ss}"
                   Foreground="{StaticResource HighlightColor}"
                   HorizontalTextAlignment="Center"
                   VerticalTextAlignment="Center"
                   Padding="2,0,0,0" />
            </StackPanel>
        </Border>

    </Grid>
</Window>
`;

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
