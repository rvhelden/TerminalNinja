import * as vscode from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind,
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

export function activate(context: vscode.ExtensionContext): void {
    const config = vscode.workspace.getConfiguration('ninja');
    const configured = config.get<string>('languageServer.path', '').trim();
    // Empty string → rely on PATH lookup. The default name is the AssemblyName
    // we set on TerminalNinja.Shell.LanguageServer.csproj.
    const serverCommand = configured.length > 0 ? configured : 'ninja-lsp';

    const serverOptions: ServerOptions = {
        run: { command: serverCommand, transport: TransportKind.stdio },
        debug: { command: serverCommand, transport: TransportKind.stdio },
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'ninja' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.ninja'),
        },
        // Surface server crash logs in the user's Output panel under "NinjaShell".
        outputChannelName: 'NinjaShell',
        traceOutputChannel: vscode.window.createOutputChannel('NinjaShell trace'),
    };

    client = new LanguageClient('ninja', 'NinjaShell', serverOptions, clientOptions);
    context.subscriptions.push({ dispose: () => client?.stop() });
    client.start();
}

export function deactivate(): Thenable<void> | undefined {
    return client?.stop();
}
