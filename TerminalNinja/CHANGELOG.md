# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

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

