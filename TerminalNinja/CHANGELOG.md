# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="1.1.0"></a>
## [1.1.0](https://www.github.com/rvhelden/TerminalNinja/releases/tag/v1.1.0) (2026-08-06)

### Features

* **charts:** add bar, line, trace, and flame graph visualizations ([063fff5](https://www.github.com/rvhelden/TerminalNinja/commit/063fff52acf24d6494e0ffbe04f2c92d89a4f26a))

<a name="1.0.0"></a>
## [1.0.0](https://www.github.com/rvhelden/TerminalNinja/releases/tag/v1.0.0) (2026-08-05)

### Features

* **buffers:** per-row grapheme cluster side table on CellBuffer ([0071c7d](https://www.github.com/rvhelden/TerminalNinja/commit/0071c7db2dcae1950e70d0e47824fc61c94734c2))
* **buffers:** widen Cell to uint Codepoint with CellFlags ([71e0fad](https://www.github.com/rvhelden/TerminalNinja/commit/71e0fad7b0abed77dd8eabdd151ad117e1c70d44))
* **controls:** bound CompletionPanel + colorized Detail + overflow ([18d6319](https://www.github.com/rvhelden/TerminalNinja/commit/18d6319009af2c7c3a52c7a06dbb5b01ec618496))
* **controls:** CompletionPanel — two-pane IntelliSense overlay ([39b7017](https://www.github.com/rvhelden/TerminalNinja/commit/39b701757ed5ca8b212a4d8eb32303a42087f59f))
* **controls:** GridSplitter — drag-to-resize panel splitter ([eb93f2f](https://www.github.com/rvhelden/TerminalNinja/commit/eb93f2f2bc6779547583ff3fbe2b80c1a4dd8032))
* **controls:** HoverPanel — floating overlay that shows any UIElement at an anchor cell ([ba8d92f](https://www.github.com/rvhelden/TerminalNinja/commit/ba8d92f6583601ce28bb4fe842e74cb859c969b2))
* **editor:** VS Code extension that connects to ninja-lsp ([94d95ad](https://www.github.com/rvhelden/TerminalNinja/commit/94d95adcb1416e634faa452822e7821667fbedb5))
* **fs:** colored category icons + dim hidden rows + curated columns ([a515b4a](https://www.github.com/rvhelden/TerminalNinja/commit/a515b4aae927046c679964e50dbc0acb30d62dda))
* **highlighting:** Function kind for known builtins ([204480a](https://www.github.com/rvhelden/TerminalNinja/commit/204480a57a6be029a38bfb7619e051705299fd12))
* **highlighting:** pluggable syntax highlighting + NinjaShell, JSON, XML ([ec29fbd](https://www.github.com/rvhelden/TerminalNinja/commit/ec29fbd1525f8473badf5584ba05e4d8ce37cf78))
* **highlighting:** RecordSyntaxHighlighter for record output ([f15c174](https://www.github.com/rvhelden/TerminalNinja/commit/f15c174952a68836add0b998690174623b431b10))
* **input:** IClipboard + SDL3 bridge + ReplView text selection ([06dd6dc](https://www.github.com/rvhelden/TerminalNinja/commit/06dd6dcfa6806266540b11288390a12ddfe05edb))
* **input:** MouseEvent carries Shift / Alt / Ctrl modifier state ([dd1d6ed](https://www.github.com/rvhelden/TerminalNinja/commit/dd1d6ed0aff1e08ba87b651541a00505cbd74818))
* **language-service:** GetHover for identifiers and module-member paths ([0ac681a](https://www.github.com/rvhelden/TerminalNinja/commit/0ac681a78a5d8512bb03da547c3a6b0580badb34))
* **printer:** record-level display + columns + row-style conventions ([2e41d21](https://www.github.com/rvhelden/TerminalNinja/commit/2e41d2150afaac67b48c9f5c8664ec1fed835c80))
* **rendering:** row-level shaped-run dispatch in Renderer.Present ([427602b](https://www.github.com/rvhelden/TerminalNinja/commit/427602bcb93b6059d9cec8163a1bc09076af3dc2))
* **sample:** bounded scrollable HoverBox + SGR-aware output rendering ([31610df](https://www.github.com/rvhelden/TerminalNinja/commit/31610dff8cabe638b2903be1089a6b1852b247d1))
* **sample:** drag-to-resize side panels via GridSplitter ([fe2c85a](https://www.github.com/rvhelden/TerminalNinja/commit/fe2c85afd25a399b74cc6906be3552e38f2e4e00))
* **sample:** editable env + scope panels in NinjaShellUi ([3b9c61d](https://www.github.com/rvhelden/TerminalNinja/commit/3b9c61dd8f08f6b3049ab8271f6cc6a593822c79))
* **sample:** highlight REPL input via SyntaxHighlighterRegistry ([bcfb7cd](https://www.github.com/rvhelden/TerminalNinja/commit/bcfb7cd7f623d5261dc7f18f75275bd01437bdfd))
* **sample:** inline ghost-text history autosuggestion ([642f8f9](https://www.github.com/rvhelden/TerminalNinja/commit/642f8f9170a74659c1d7f41557b2b244bd621c12))
* **sample:** LSP-powered diagnostics, hover, and Tab completion in ReplView ([24f0629](https://www.github.com/rvhelden/TerminalNinja/commit/24f0629b806575f7615a1bb410d8b1cd0bd815e1))
* **sample:** multi-line REPL input via Shift+Enter ([d26764c](https://www.github.com/rvhelden/TerminalNinja/commit/d26764cf7dbb5c619c1e84ce8c34f10b8259972c))
* **sample:** NinjaShellUi — Skia-hosted shell with toggleable side panels ([6cecce1](https://www.github.com/rvhelden/TerminalNinja/commit/6cecce13acd3ffd7ad8a32c9eb5b720c1abe928c))
* **sample:** ReplView mouse hover — shape + data for input and output ([cd666fc](https://www.github.com/rvhelden/TerminalNinja/commit/cd666fcfb3a99745f485819cd7c83fc2c79cc1f8))
* **sample:** ReplView vertical scrolling — wheel, PageUp/Down, Ctrl+Home/End, indicator ([5c2ff33](https://www.github.com/rvhelden/TerminalNinja/commit/5c2ff33a214816c14a7f4c7e04836f9f6d53f01f))
* **sample:** scope panel hides builtins, shows only user bindings ([1e47015](https://www.github.com/rvhelden/TerminalNinja/commit/1e47015c350e72969fb5d505ecd0610963fe6889))
* **sample:** wire CompletionPanel + signature help into ReplView ([2911fb7](https://www.github.com/rvhelden/TerminalNinja/commit/2911fb79b1b040a890817555d888b08b403bb4ab))
* **shell:** completion service + LSP textDocument/completion handler ([97cc9e9](https://www.github.com/rvhelden/TerminalNinja/commit/97cc9e95579ec741465e411b5128b1122794ce44))
* **shell:** document symbols — outline view via LanguageService + LSP ([844b736](https://www.github.com/rvhelden/TerminalNinja/commit/844b736079991c5e7a00d0b9f1c10ab20e004354))
* **shell:** enrich CompletionItem with Documentation (shape + data for scope) ([e53ed9f](https://www.github.com/rvhelden/TerminalNinja/commit/e53ed9fdd3ff6a68eedcc59e5634f70f0fd420e2))
* **shell:** env module — process-scoped environment variable access ([a233883](https://www.github.com/rvhelden/TerminalNinja/commit/a2338835ea78d6dfc75dba3d0e96419f893ef239))
* **shell:** Env.TrySetBindingValue for in-place scope mutation ([0ca69e9](https://www.github.com/rvhelden/TerminalNinja/commit/0ca69e96f7507927bb6e37e97926a84e021b7cdf))
* **shell:** evaluator, immutable env, and pipeline builtins ([e771f75](https://www.github.com/rvhelden/TerminalNinja/commit/e771f75f7abc9c0979d0019d280d3bc7f6aa92ec))
* **shell:** expose Env.Bindings for scope introspection ([2df7a87](https://www.github.com/rvhelden/TerminalNinja/commit/2df7a879deb36a53cd7b1c886a967856e79d6a65))
* **shell:** fs module — migrate flat ls/cd/pwd/cat + add read/write/mkdir/rm/move/copy ([b38a888](https://www.github.com/rvhelden/TerminalNinja/commit/b38a888bb770f36364b7b1713c31446c11c424a7))
* **shell:** json module — parse / stringify ([1fe55a2](https://www.github.com/rvhelden/TerminalNinja/commit/1fe55a2e998b581e5b5af2c4f4b88890d6a918cd))
* **shell:** keyed sort with options-record + reverse builtin ([5d3ccaf](https://www.github.com/rvhelden/TerminalNinja/commit/5d3ccaf7b2c16d896cd4fbd712229adfd7912f31))
* **shell:** lazy NSeq pipelines — ranges and where/select/take/skip/head stream ([af35468](https://www.github.com/rvhelden/TerminalNinja/commit/af35468a3b4cde8d6291e67009a27564cba2fcc4))
* **shell:** multi-error parsing — collect every diagnostic, not just the first ([447125d](https://www.github.com/rvhelden/TerminalNinja/commit/447125d601ccc17ce07a77481b3d29e49e9bae31))
* **shell:** multi-statement scripts ([55883f4](https://www.github.com/rvhelden/TerminalNinja/commit/55883f4837608997c1da1b19d761e9bd436808a0))
* **shell:** NinjaShell language server with shared LanguageService API ([b9aef48](https://www.github.com/rvhelden/TerminalNinja/commit/b9aef48e446cb0aae1b7a39d4c4248b03717d94d))
* **shell:** NinjaShell lexer with interpolation and pwsh-block scanner ([f40818b](https://www.github.com/rvhelden/TerminalNinja/commit/f40818ba4b88fbbcb4127d74b4a780d6224ae648))
* **shell:** NinjaShell parser, AST, and pretty-printer ([8d84cee](https://www.github.com/rvhelden/TerminalNinja/commit/8d84cee4ef33d22122d6d7aa292eb9bc773f043d))
* **shell:** obj module — type / size / dump / def ([0e1bb29](https://www.github.com/rvhelden/TerminalNinja/commit/0e1bb29b49c1d1d9306189b95b4012d05b46c59d))
* **shell:** obj record/table conversion helpers ([378ee05](https://www.github.com/rvhelden/TerminalNinja/commit/378ee05b305860a1a4d0ea91595808199c9c0efa))
* **shell:** obj.dump as vertical property table + obj.table ([dcd9ac2](https://www.github.com/rvhelden/TerminalNinja/commit/dcd9ac238732c27656a169de9f96758474131487))
* **shell:** PowerShell subprocess bridge with JSON channel ([7a11e31](https://www.github.com/rvhelden/TerminalNinja/commit/7a11e314dbbd581f0f959e644775c54f4a3bcdb9))
* **shell:** proc module — current-process introspection and lifecycle ([566ac14](https://www.github.com/rvhelden/TerminalNinja/commit/566ac149d8632e6408ec9c3d6a85a37ed232fa7c))
* **shell:** ragged tables — missing cells render as blank, obj.normalize fills defaults ([43e8f31](https://www.github.com/rvhelden/TerminalNinja/commit/43e8f3130497135b37e59b9cf7b7215486786ccc))
* **shell:** record-field completion + interpolation-hole completion ([88292f8](https://www.github.com/rvhelden/TerminalNinja/commit/88292f855ae4cd589de1d92ad576d06587905797))
* **shell:** REPL loop, line accumulator, FS/IO builtins, and table printer ([e7e96e5](https://www.github.com/rvhelden/TerminalNinja/commit/e7e96e52ff5c59cbeb37f028fad2810b049012a8))
* **shell:** REPL Tab completion via shared LanguageService ([6298941](https://www.github.com/rvhelden/TerminalNinja/commit/6298941680a7f14a4ed74f60faa9b1cb888e1d20))
* **shell:** scaffold TerminalNinja.Shell project with NValue C# 15 union ([a175579](https://www.github.com/rvhelden/TerminalNinja/commit/a1755792f60ce9ba31ea30ee9caafa3ad5195b7d))
* **shell:** scope-aware completions — surface user `let` bindings ([2905d89](https://www.github.com/rvhelden/TerminalNinja/commit/2905d897d51bf7223b2a22cb3b51fe0e6cdc30a7))
* **shell:** signature help — parameter hints inside open parens ([211bcd9](https://www.github.com/rvhelden/TerminalNinja/commit/211bcd97dc022d42fd2e079abee30b297e726452))
* **shell:** source keyword for evaluating a script file in the current scope ([33c17bb](https://www.github.com/rvhelden/TerminalNinja/commit/33c17bbfc08819e2d5bb7f7730b8767d48ed096f))
* **shell:** Span tracking on every AST node ([b270f26](https://www.github.com/rvhelden/TerminalNinja/commit/b270f2600d2f5754a9381a725ef7bc393a9a3786))
* **shell:** widen diagnostic ranges + inline error underline in REPL ([ffd82ef](https://www.github.com/rvhelden/TerminalNinja/commit/ffd82ef4b16ba6dde11b7b5051d799ab675bdcff))
* **shell:** xml module — doc / save / find / find_all / text / attr / xpath ([5d7e1fd](https://www.github.com/rvhelden/TerminalNinja/commit/5d7e1fd0f39f82000498fadae1f66b123e5cdca4))
* **skia:** add IShapedRunSink + HarfBuzz shaping path ([aa96ccb](https://www.github.com/rvhelden/TerminalNinja/commit/aa96ccbd0abedfc0d647ced300f25a9ddb7604b7))
* **skia:** add TerminalNinja.Skia with SkiaCellSink + SDL3 host ([36b48ee](https://www.github.com/rvhelden/TerminalNinja/commit/36b48ee611b33f4408195cad24ecd2e84932d1d2))
* **skia:** bold / italic decorations via on-demand typeface variants ([ca44084](https://www.github.com/rvhelden/TerminalNinja/commit/ca44084f1fc28ddd19906547d194478ef6c83fc2))
* **skia:** HiDPI / display-scale-aware rendering ([2fb6e1d](https://www.github.com/rvhelden/TerminalNinja/commit/2fb6e1d34dfb534266b41ffbc318857003176dae))
* **skia:** SDL3 input backend + FocusManager dispatch + shape cache ([3ab47f1](https://www.github.com/rvhelden/TerminalNinja/commit/3ab47f1ccc396400588cc761e6f05a28748dca7c))
* **skia:** SDL3 text-input event path for shifted symbols and IME ([9a7de7a](https://www.github.com/rvhelden/TerminalNinja/commit/9a7de7a6d1920cd8cc5caa571addba0a8459237e))
* **skia:** SkiaApplication wraps Application so controls see Current ([96545cf](https://www.github.com/rvhelden/TerminalNinja/commit/96545cfc3edf090a681758e9a2af57beb951cfe3))
* **terminal:** add TerminalNinja.Terminal project with ITerminalBackend skeleton ([191a353](https://www.github.com/rvhelden/TerminalNinja/commit/191a353ef7540aa2ad5156fbe0ea4fb71b998faa))
* **terminal:** ConPtyTerminalBackend — real Windows pseudo-console backend ([4bfa1ed](https://www.github.com/rvhelden/TerminalNinja/commit/4bfa1edbfe4d0206c2df9b0967ea9dc406d154b6))
* **terminal:** save/restore cursor + in-place character ops in screen buffer ([c83976f](https://www.github.com/rvhelden/TerminalNinja/commit/c83976f8ceff596758537a68b1364e121298fdd0))
* **terminal:** TerminalScreenBuffer — parser handler that maintains cell grid ([18994f9](https://www.github.com/rvhelden/TerminalNinja/commit/18994f9cde9187b7cf964d705fde2a2ae63dc7f4))
* **terminal:** TerminalView control + key-event encoder ([92c6b4c](https://www.github.com/rvhelden/TerminalNinja/commit/92c6b4c2d4ff661439c32e415c05ec4db24efd8a))
* **terminal:** VT/ANSI escape-sequence parser ([fd81378](https://www.github.com/rvhelden/TerminalNinja/commit/fd81378610a418a49b1e4d3bad7c7c52ad593fd0))
* **xaml:** support {Binding} on attached properties ([414a188](https://www.github.com/rvhelden/TerminalNinja/commit/414a188010b2ec99e44b235724f51698e20304f4))

### Bug Fixes

* **app:** propagate EscapeQuits so Esc reaches focused controls ([9ef356c](https://www.github.com/rvhelden/TerminalNinja/commit/9ef356cb0723bb94bba948fdac747eca9473807b))
* **sample:** make copy actually work — clear selection, surface failures, right-click ([d4f88ff](https://www.github.com/rvhelden/TerminalNinja/commit/d4f88ffde7e55f8e643dc0c7197d3d39b82a184e))
* **sample:** widen Files / right panel right-padding to avoid border crowding ([f785dc9](https://www.github.com/rvhelden/TerminalNinja/commit/f785dc951d50227c6328d4714c2439c2703b550d))
* **skia:** pin glyphs to source-cluster cell × cellWidth (round wasn't enough) ([694aa7a](https://www.github.com/rvhelden/TerminalNinja/commit/694aa7a2ef2409044f511ab94f8c79010920aeb0))
* **skia:** snap shaped-glyph X positions to the cell grid ([a1d3a16](https://www.github.com/rvhelden/TerminalNinja/commit/a1d3a1633e1831a4e3fe3efa8d6b91526294a105))
* **skia:** stop duplicating printable symbols between KEY_DOWN and TEXT_INPUT ([1bcade8](https://www.github.com/rvhelden/TerminalNinja/commit/1bcade84ba1b134b3ad33cbea14dd6513e79d06e))
* **terminal:** set STARTF_USESTDHANDLES so cmd attaches to the pseudoconsole ([31d2373](https://www.github.com/rvhelden/TerminalNinja/commit/31d23739248a8f7900187671e403bcc2f92e726c))

### Breaking Changes

* **buffers:** widen Cell to uint Codepoint with CellFlags ([71e0fad](https://www.github.com/rvhelden/TerminalNinja/commit/71e0fad7b0abed77dd8eabdd151ad117e1c70d44))
* **controls:** split Render into Visibility-aware wrapper + OnRender ([41ed550](https://www.github.com/rvhelden/TerminalNinja/commit/41ed5500ef36cf9c880836f9817571944c636e4c))
* **shell:** fs module — migrate flat ls/cd/pwd/cat + add read/write/mkdir/rm/move/copy ([b38a888](https://www.github.com/rvhelden/TerminalNinja/commit/b38a888bb770f36364b7b1713c31446c11c424a7))

<a name="0.1.0"></a>
## [0.1.0](https://www.github.com/rvhelden/TerminalNinja/releases/tag/v0.1.0) (2026-04-18)

### Features

* add CheckBox, RadioButton, and ComboBox controls with theming ([cf6bdbc](https://www.github.com/rvhelden/TerminalNinja/commit/cf6bdbcc032f76b956993306303ab4d52510d06f))
* add CLI ANSI snapshot tool and remove WPF dependency ([#1](https://www.github.com/rvhelden/TerminalNinja/issues/1)) ([0231063](https://www.github.com/rvhelden/TerminalNinja/commit/023106369bf111a3959b562014f4c9906900a215))
* add ColorPicker and Image controls with Stretch enum ([bbbb95b](https://www.github.com/rvhelden/TerminalNinja/commit/bbbb95bcdf38aece5bf21694de2a168fafb7daef))
* add custom theme loading, Margin DP, and Padding improvements ([be12c0b](https://www.github.com/rvhelden/TerminalNinja/commit/be12c0b26e2b763676b0fa02b6fd91c7dcb7afe9))
* add DataGrid, FilePicker, and FolderPicker — complete roadmap ([7097203](https://www.github.com/rvhelden/TerminalNinja/commit/7097203a846058754b0f35dc879b0b5dd1d487f2))
* add NumberPicker, DatePicker, TimePicker, and DateTimePicker ([a9ca41c](https://www.github.com/rvhelden/TerminalNinja/commit/a9ca41c4e5b27bb23d15e03767cae36bf61947c3))
* add ScrollViewer and TextBox controls ([0bd8278](https://www.github.com/rvhelden/TerminalNinja/commit/0bd8278b10d8832ce24c83299d995e16b4700e09))
* add TabControl, TreeView, and ListView controls with theming ([68cf9a2](https://www.github.com/rvhelden/TerminalNinja/commit/68cf9a2e97ae93ca41a7921a55561a7bd6676cdd))
* add XAML→ANSI WASM module and live playground ([#2](https://www.github.com/rvhelden/TerminalNinja/issues/2)) ([750adfa](https://www.github.com/rvhelden/TerminalNinja/commit/750adfa03508ea00652e3fe94539299e79a51872))
* auto-enable XAML hot reload when debugger is attached ([743ed9c](https://www.github.com/rvhelden/TerminalNinja/commit/743ed9cf553e4753bbd4d513cc9379a6f377faab))
* HSL-based ColorPickerDialog with hue bar + SL gradient ([eb12ac9](https://www.github.com/rvhelden/TerminalNinja/commit/eb12ac9156398e54fafa914937ccbb9a52bada19))
* ListBox built-in scrolling — no ScrollViewer wrapper needed ([ef215d0](https://www.github.com/rvhelden/TerminalNinja/commit/ef215d0cd3d7046cc8af6b630c12b12aa5f51b65))
* playground live XAML reload, mouse wheel, responsive resize ([79b8393](https://www.github.com/rvhelden/TerminalNinja/commit/79b839369833a36f8629a6ed61c0ad38d8d6bb69))
* XAML hot reload for live UI editing during development ([056c043](https://www.github.com/rvhelden/TerminalNinja/commit/056c0430b5364e7204e293e798a3426e6ae6dde1))

### Bug Fixes

* ColorPickerDialog — arrow keys return to hue bar, cursor clamped ([73a6c61](https://www.github.com/rvhelden/TerminalNinja/commit/73a6c610b20f6429ea44109b0f48b1b60c5d8cdd))
* ColorPickerDialog — Up/Down navigates between hue bar and SL grid ([e5c915c](https://www.github.com/rvhelden/TerminalNinja/commit/e5c915ce026e00245359a4510f93cbdae0bc0f0f))
* ComboBox display text, dropdown closing, and TabControl auto-select ([13d3b43](https://www.github.com/rvhelden/TerminalNinja/commit/13d3b43ba0c82d4bc29180dbaa78719a3e6bff54))
* correct WASM AppBundle output path in CI workflow ([#6](https://www.github.com/rvhelden/TerminalNinja/issues/6)) ([86abd7a](https://www.github.com/rvhelden/TerminalNinja/commit/86abd7a4e4c85864feb4dd7ec865a186f92af8eb))
* dialogs sample — show as layout preview with usage code, not fake inline dialog ([f0b655e](https://www.github.com/rvhelden/TerminalNinja/commit/f0b655e3c98f684735ac2c673b15a49689da9ffd))
* focus visibility — add focus borders to list controls, fix ScrollViewer ([65a09c6](https://www.github.com/rvhelden/TerminalNinja/commit/65a09c6f06f715976184fcedb9b5f02b30db2526))
* ListBox scrolls parent ScrollViewer to keep selection visible ([5f04fd0](https://www.github.com/rvhelden/TerminalNinja/commit/5f04fd0033b7a7a4b8cdde063260744e1151daf3))
* playground auto-sizes terminal from container, not fixed inputs ([55d7ff5](https://www.github.com/rvhelden/TerminalNinja/commit/55d7ff5005bd929997761f2df81a4e9012b93525))
* RadioButton arrow focus, ListBox double-click, ListView default column ([60d64a9](https://www.github.com/rvhelden/TerminalNinja/commit/60d64a9f52329c3055f82767eada73c50c9bc193))
* reset cursor tracking between frames to prevent rendering corruption ([019edef](https://www.github.com/rvhelden/TerminalNinja/commit/019edef593d67f9e1237d9e1ed370683c4511bc1))
* resolve WASM build errors CS8805 and CA1416 ([#4](https://www.github.com/rvhelden/TerminalNinja/issues/4)) ([68ee03e](https://www.github.com/rvhelden/TerminalNinja/commit/68ee03e04a6207ab7f4225708662a51f4a2992ed))
* use dotnet publish to include .NET WASM runtime in AppBundle ([#5](https://www.github.com/rvhelden/TerminalNinja/issues/5)) ([c5bf981](https://www.github.com/rvhelden/TerminalNinja/commit/c5bf981ca59df8541f8dbd2484cfa2be6ef0c132))
* wire ListBox.ItemActivated to main menu navigation ([813509d](https://www.github.com/rvhelden/TerminalNinja/commit/813509d824f00cc3119e1a848c11f3e768470e2c))

