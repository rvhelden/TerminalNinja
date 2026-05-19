using TerminalNinja.Shell.Skia.Branding;

// Tiny CLI wrapper around IconRenderer. The host (TerminalNinja.Shell.Skia) renders the
// same icon at runtime to set as the SDL window icon; this one-off baker is the path used
// to dump a static PNG for places that need a file on disk — e.g. the VS Code extension's
// marketplace tile. Lives outside the main solution graph so it builds independently of
// the Shell project's mid-refactor state.
if (args.Length < 1)
{
    Console.Error.WriteLine("usage: IconBaker <out-path> [size]");
    return 1;
}

var size = args.Length >= 2 && int.TryParse(args[1], out var parsed) ? parsed : 256;
IconRenderer.WritePng(args[0], size);
Console.WriteLine($"Wrote {size}×{size} icon to {args[0]}");
return 0;
