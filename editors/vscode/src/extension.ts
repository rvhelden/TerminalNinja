import * as vscode from 'vscode';
import * as fs from 'fs';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind,
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;
let outputChannel: vscode.OutputChannel | undefined;

export function activate(context: vscode.ExtensionContext): void {
    outputChannel = vscode.window.createOutputChannel('NinjaShell');
    context.subscriptions.push(outputChannel);

    const config = vscode.workspace.getConfiguration('ninja');
    const configured = config.get<string>('languageServer.path', '').trim();
    // Empty string → rely on PATH lookup. The default name is the AssemblyName
    // we set on TerminalNinja.Shell.LanguageServer.csproj.
    const serverCommand = configured.length > 0 ? configured : 'ninja-lsp';

    outputChannel.appendLine(`[ninjashell] Spawning language server: ${serverCommand}`);
    if (configured.length > 0 && !fs.existsSync(configured)) {
        const msg = `ninja.languageServer.path points to a file that does not exist: ${configured}`;
        outputChannel.appendLine(`[ninjashell] ${msg}`);
        void vscode.window.showErrorMessage(`NinjaShell: ${msg}`);
        return;
    }

    const serverOptions: ServerOptions = {
        run: { command: serverCommand, transport: TransportKind.stdio },
        debug: { command: serverCommand, transport: TransportKind.stdio },
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'ninja' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.ninja'),
        },
        outputChannel,
        traceOutputChannel: vscode.window.createOutputChannel('NinjaShell trace'),
    };

    client = new LanguageClient('ninja', 'NinjaShell', serverOptions, clientOptions);
    context.subscriptions.push({ dispose: () => client?.stop() });
    client.start().then(
        () => outputChannel?.appendLine('[ninjashell] Language server started.'),
        (err) => {
            const message = err instanceof Error ? err.message : String(err);
            outputChannel?.appendLine(`[ninjashell] Failed to start language server: ${message}`);
            outputChannel?.appendLine(
                `[ninjashell] Hint: verify '${serverCommand}' is on PATH and runs without crashing. ` +
                `If you're using an AOT publish, the binary should be at ` +
                `TerminalNinja.Shell.LanguageServer/bin/Release/net11.0/<rid>/publish/ninja-lsp(.exe). ` +
                `Otherwise set ninja.languageServer.path to its absolute location.`,
            );
            void vscode.window.showErrorMessage(
                `NinjaShell language server failed to start: ${message}. ` +
                `See the NinjaShell output channel for details.`,
            );
        },
    );

    // ── Debug adapter wiring ─────────────────────────────────────────────
    // `ninja --dap` exposes the interpreter via the Debug Adapter Protocol.
    // The binary is the same `ninja` shell binary published from
    // TerminalNinja.Shell, so we reuse the language-server path by default
    // and let the user override via `ninja.debugAdapter.path`.
    const adapterConfigured = config.get<string>('debugAdapter.path', '').trim();
    const adapterCommand = adapterConfigured.length > 0 ? adapterConfigured : 'ninja';

    if (adapterConfigured.length > 0 && !fs.existsSync(adapterConfigured)) {
        const msg = `ninja.debugAdapter.path points to a file that does not exist: ${adapterConfigured}`;
        outputChannel.appendLine(`[ninjashell] ${msg}`);
        void vscode.window.showWarningMessage(`NinjaShell: ${msg}`);
    }

    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('ninja', {
            createDebugAdapterDescriptor: () => {
                outputChannel?.appendLine(`[ninjashell] Spawning debug adapter: ${adapterCommand} --dap`);
                return new vscode.DebugAdapterExecutable(adapterCommand, ['--dap']);
            },
        }),
    );

    // Resolve `${file}` etc. and fill in defaults if launch.json omits them.
    context.subscriptions.push(
        vscode.debug.registerDebugConfigurationProvider('ninja', {
            resolveDebugConfiguration: (_folder, cfg) => {
                if (!cfg.type && !cfg.request && !cfg.name) {
                    // No launch.json — synthesize one for the active editor.
                    const editor = vscode.window.activeTextEditor;
                    if (editor && editor.document.languageId === 'ninja') {
                        cfg.type = 'ninja';
                        cfg.request = 'launch';
                        cfg.name = 'Run NinjaShell file';
                        cfg.program = editor.document.uri.fsPath;
                    }
                }
                if (!cfg.program) {
                    void vscode.window.showErrorMessage('NinjaShell debug: launch.json must set `program` to a .ninja file path.');
                    return undefined; // abort
                }
                return cfg;
            },
        }),
    );
}

export function deactivate(): Thenable<void> | undefined {
    return client?.stop();
}
