using System.Collections.Immutable;
using System.Globalization;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>fs</c> module — filesystem operations. Replaces the flat <c>ls</c> /
/// <c>cd</c> / <c>pwd</c> / <c>cat</c> builtins from earlier MVPs. Paths are
/// resolved via <see cref="Path.GetFullPath(string)"/> against the current working
/// directory before any disk operation.
/// </summary>
public static class FsModule
{
    /// <summary>Register the <c>fs</c> module into the default-environment builder.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        BuiltinRegistry.RegisterModule(b, "fs",
            ("pwd", new NFunc(Pwd, 0)),
            ("cd", new NFunc(Cd, 1)),
            ("ls", new NFunc(Ls, -1)),
            ("cat", new NFunc(Cat, 1)),
            ("read", new NFunc(Cat, 1)),
            ("write", new NFunc(Write, 2)),
            ("append", new NFunc(Append, 2)),
            ("exists", new NFunc(Exists, 1)),
            ("is_dir", new NFunc(IsDir, 1)),
            ("mkdir", new NFunc(Mkdir, -1)),
            ("rm", new NFunc(Rm, -1)),
            ("move", new NFunc(Move, 2)),
            ("copy", new NFunc(Copy, 2)));
    }

    private static NValue Pwd(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"fs.pwd expects 0 arguments, got {args.Length}");
        return new NString(Directory.GetCurrentDirectory());
    }

    private static NValue Cd(NValue[] args)
    {
        var path = RequirePath(args, "fs.cd");
        if (!Directory.Exists(path)) throw new EvaluatorException($"fs.cd: directory not found: {path}");
        Directory.SetCurrentDirectory(path);
        return new NString(Directory.GetCurrentDirectory());
    }

    private static NValue Ls(NValue[] args)
    {
        string target = Directory.GetCurrentDirectory();
        NRecord? options = null;

        if (args.Length >= 1)
        {
            if (args[0] is NString path) target = Path.GetFullPath(path.Value);
            else if (args[0] is NRecord opts) options = opts;
            else throw new EvaluatorException("fs.ls first arg must be a string path or options record");
        }
        if (args.Length == 2)
        {
            if (args[1] is NRecord opts) options = opts;
            else throw new EvaluatorException("fs.ls second arg must be an options record");
        }
        if (args.Length > 2) throw new EvaluatorException($"fs.ls expects 0..2 arguments, got {args.Length}");
        if (!Directory.Exists(target)) throw new EvaluatorException($"fs.ls: directory not found: {target}");

        bool recurse = ReadBool(options, "recurse", false);
        bool hidden = ReadBool(options, "hidden", false);
        string? pattern = ReadString(options, "pattern", null);

        var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var searchPattern = pattern ?? "*";

        var b = ImmutableArray.CreateBuilder<NValue>();
        foreach (var dir in Directory.EnumerateDirectories(target, searchPattern, searchOption))
        {
            if (!hidden && IsHidden(dir)) continue;
            b.Add(MakeEntry(dir, isDir: true, size: 0));
        }
        foreach (var file in Directory.EnumerateFiles(target, searchPattern, searchOption))
        {
            if (!hidden && IsHidden(file)) continue;
            long size = 0;
            try { size = new FileInfo(file).Length; } catch { /* unreadable — keep 0 */ }
            b.Add(MakeEntry(file, isDir: false, size: size));
        }
        return new NList(b.ToImmutable());
    }

    private static NValue Cat(NValue[] args)
    {
        var path = RequirePath(args, "fs.cat");
        if (!File.Exists(path)) throw new EvaluatorException($"fs.cat: file not found: {path}");
        return new NString(File.ReadAllText(path));
    }

    private static NValue Write(NValue[] args)
    {
        if (args.Length != 2) throw new EvaluatorException($"fs.write expects 2 arguments, got {args.Length}");
        if (args[0] is not NString p) throw new EvaluatorException("fs.write path must be a string");
        if (args[1] is not NString content) throw new EvaluatorException("fs.write content must be a string");
        File.WriteAllText(Path.GetFullPath(p.Value), content.Value);
        return NUnit.Instance;
    }

    private static NValue Append(NValue[] args)
    {
        if (args.Length != 2) throw new EvaluatorException($"fs.append expects 2 arguments, got {args.Length}");
        if (args[0] is not NString p) throw new EvaluatorException("fs.append path must be a string");
        if (args[1] is not NString content) throw new EvaluatorException("fs.append content must be a string");
        File.AppendAllText(Path.GetFullPath(p.Value), content.Value);
        return NUnit.Instance;
    }

    private static NValue Exists(NValue[] args)
    {
        var path = RequirePath(args, "fs.exists");
        return new NBool(File.Exists(path) || Directory.Exists(path));
    }

    private static NValue IsDir(NValue[] args)
    {
        var path = RequirePath(args, "fs.is_dir");
        return new NBool(Directory.Exists(path));
    }

    private static NValue Mkdir(NValue[] args)
    {
        if (args.Length is < 1 or > 2) throw new EvaluatorException($"fs.mkdir expects 1 or 2 arguments, got {args.Length}");
        if (args[0] is not NString p) throw new EvaluatorException("fs.mkdir path must be a string");
        NRecord? options = null;
        if (args.Length == 2)
        {
            if (args[1] is NRecord r) options = r;
            else throw new EvaluatorException("fs.mkdir options must be a record");
        }

        bool recursive = ReadBool(options, "recursive", false);
        var path = Path.GetFullPath(p.Value);
        if (Directory.Exists(path)) return NUnit.Instance;
        if (!recursive && Directory.GetParent(path) is { } parent && !Directory.Exists(parent.FullName))
            throw new EvaluatorException($"fs.mkdir: parent directory does not exist: {parent.FullName} (pass {{ recursive: true }})");
        Directory.CreateDirectory(path);
        return NUnit.Instance;
    }

    private static NValue Rm(NValue[] args)
    {
        if (args.Length is < 1 or > 2) throw new EvaluatorException($"fs.rm expects 1 or 2 arguments, got {args.Length}");
        if (args[0] is not NString p) throw new EvaluatorException("fs.rm path must be a string");
        NRecord? options = null;
        if (args.Length == 2)
        {
            if (args[1] is NRecord r) options = r;
            else throw new EvaluatorException("fs.rm options must be a record");
        }

        bool recursive = ReadBool(options, "recursive", false);
        bool force = ReadBool(options, "force", false);
        var path = Path.GetFullPath(p.Value);

        if (Directory.Exists(path))
        {
            if (!recursive)
                throw new EvaluatorException($"fs.rm: '{path}' is a directory; pass {{ recursive: true }} to remove");
            Directory.Delete(path, recursive: true);
            return NUnit.Instance;
        }
        if (File.Exists(path))
        {
            File.Delete(path);
            return NUnit.Instance;
        }
        if (force) return NUnit.Instance;
        throw new EvaluatorException($"fs.rm: path does not exist: {path}");
    }

    private static NValue Move(NValue[] args)
    {
        if (args.Length != 2) throw new EvaluatorException($"fs.move expects 2 arguments, got {args.Length}");
        if (args[0] is not NString s) throw new EvaluatorException("fs.move source must be a string");
        if (args[1] is not NString d) throw new EvaluatorException("fs.move destination must be a string");
        var src = Path.GetFullPath(s.Value);
        var dst = Path.GetFullPath(d.Value);
        if (Directory.Exists(src)) Directory.Move(src, dst);
        else File.Move(src, dst);
        return NUnit.Instance;
    }

    private static NValue Copy(NValue[] args)
    {
        if (args.Length != 2) throw new EvaluatorException($"fs.copy expects 2 arguments, got {args.Length}");
        if (args[0] is not NString s) throw new EvaluatorException("fs.copy source must be a string");
        if (args[1] is not NString d) throw new EvaluatorException("fs.copy destination must be a string");
        var src = Path.GetFullPath(s.Value);
        var dst = Path.GetFullPath(d.Value);
        if (Directory.Exists(src)) throw new EvaluatorException("fs.copy on directories is not supported in MVP — use fs.move or copy files individually");
        File.Copy(src, dst, overwrite: true);
        return NUnit.Instance;
    }

    private static string RequirePath(NValue[] args, string op)
    {
        if (args.Length != 1) throw new EvaluatorException($"{op} expects 1 argument, got {args.Length}");
        if (args[0] is not NString s) throw new EvaluatorException($"{op} path must be a string");
        return Path.GetFullPath(s.Value);
    }

    private static bool ReadBool(NRecord? r, string key, bool defaultValue)
    {
        if (r == null) return defaultValue;
        if (!r.Fields.TryGetValue(key, out var v)) return defaultValue;
        if (v is NBool b) return b.Value;
        throw new EvaluatorException($"option '{key}' must be a bool");
    }

    private static string? ReadString(NRecord? r, string key, string? defaultValue)
    {
        if (r == null) return defaultValue;
        if (!r.Fields.TryGetValue(key, out var v)) return defaultValue;
        if (v is NString s) return s.Value;
        throw new EvaluatorException($"option '{key}' must be a string");
    }

    private static bool IsHidden(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith('.')) return true;
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Hidden) != 0;
        }
        catch { return false; }
    }

    private static NValue MakeEntry(string fullPath, bool isDir, long size)
    {
        var name = Path.GetFileName(fullPath);
        var hidden = name.StartsWith('.');
        var d = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        d["Name"] = new NString(name);
        d["FullPath"] = new NString(fullPath);
        d["IsDirectory"] = new NBool(isDir);
        d["IsHidden"] = new NBool(hidden);
        d["Size"] = new NInt(size);
        d["SizeText"] = new NString(FormatSize(size, isDir));
        // Icon carries the glyph wrapped in an SGR truecolor escape so the table
        // renderer surfaces it colored per category. The escape pair is opaque to
        // the data layer — Printer's column-width math strips SGR before
        // measuring, and obj.dump shows the raw string honestly.
        d["Icon"] = new NString(ColorizedIcon(name, isDir));
        try
        {
            var modified = isDir ? Directory.GetLastWriteTimeUtc(fullPath) : File.GetLastWriteTimeUtc(fullPath);
            d["LastModified"] = new NString(modified.ToString("o", CultureInfo.InvariantCulture));
        }
        catch
        {
            d["LastModified"] = NUnit.Instance;
        }
        // __display tells Printer.Format which field carries the canonical text
        // representation when a single entry prints on its own. The value is the
        // name of the field to render, not the rendered text — that way derived
        // records (e.g. from `fs.ls() | select …`) can override which field
        // surfaces as the default.
        d["__display"] = new NString("FullPath");
        // __row_style="dim" tells the table renderer to wrap the entire row in
        // SGR dim (\e[2m), marking the entry as hidden. The Skia sink maps that
        // to TextDecorations.Dim and renders the row at halved alpha.
        if (hidden) d["__row_style"] = new NString("dim");
        // __columns pins the default table view: icon (category color), name,
        // size. Type column dropped — the colored icon already conveys category
        // and saves horizontal space in narrow REPL panels. Richer fields
        // (FullPath, IsDirectory, IsHidden, LastModified, raw Size) stay
        // reachable via dot-access.
        d["__columns"] = new NList(ImmutableArray.Create<NValue>(
            new NString("Icon"),
            new NString("Name"),
            new NString("SizeText")));
        return new NRecord(d.ToImmutable());
    }

    /// <summary>
    /// Returns the entry's icon glyph wrapped in an SGR truecolor escape. Nerd
    /// Font codepoints (FiraCode Nerd Font Mono in the Skia sample) render the
    /// glyph; the SGR pair sets the foreground per category and resets it.
    /// </summary>
    private static string ColorizedIcon(string name, bool isDir)
    {
        var (glyph, color) = ClassifyEntry(name, isDir);
        return $"\x1b[38;2;{color.R};{color.G};{color.B}m{glyph}\x1b[39m";
    }

    /// <summary>
    /// Map a filesystem entry to its display glyph + accent color. Colors come
    /// from the Catppuccin Mocha palette so they harmonise with the Dark theme
    /// the Skia sample ships with.
    /// </summary>
    private static (string Glyph, (byte R, byte G, byte B) Color) ClassifyEntry(string name, bool isDir)
    {
        (byte R, byte G, byte B) blue   = (0x89, 0xB4, 0xFA);
        (byte R, byte G, byte B) mauve  = (0xCB, 0xA6, 0xF7);
        (byte R, byte G, byte B) green  = (0xA6, 0xE3, 0xA1);
        (byte R, byte G, byte B) yellow = (0xF9, 0xE2, 0xAF);
        (byte R, byte G, byte B) peach  = (0xFA, 0xB3, 0x87);
        (byte R, byte G, byte B) pink   = (0xF5, 0xC2, 0xE7);
        (byte R, byte G, byte B) red    = (0xF3, 0x8B, 0xA8);
        (byte R, byte G, byte B) teal   = (0x94, 0xE2, 0xD5);
        (byte R, byte G, byte B) gray   = (0xA6, 0xAD, 0xC8);

        if (isDir) return (FolderGlyph, blue);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" or ".webp" or ".bmp"
                => (ImageGlyph, pink),
            ".cs" or ".ts" or ".tsx" or ".js" or ".jsx" or ".py" or ".go" or ".rs"
                or ".java" or ".cpp" or ".c" or ".h" or ".rb" or ".php" or ".swift" or ".kt"
                => (CodeGlyph, mauve),
            ".md" or ".markdown" => (MarkdownGlyph, teal),
            ".json" => (JsonGlyph, yellow),
            ".xml" or ".xaml" => (CodeGlyph, peach),
            ".html" or ".htm" => (CodeGlyph, red),
            ".yaml" or ".yml" or ".toml" or ".ini" or ".cfg" or ".config"
                => (ConfigGlyph, yellow),
            ".zip" or ".tar" or ".gz" or ".7z" or ".rar" => (ArchiveGlyph, peach),
            ".pdf" => (PdfGlyph, red),
            ".txt" or ".log" => (TextGlyph, green),
            _ => (FileGlyph, gray),
        };
    }

    // Nerd Font glyph constants. Kept as named constants so the ClassifyEntry
    // switch stays readable; the actual codepoints are private-use ones from
    // FiraCode Nerd Font Mono.
    private const string FolderGlyph   = "";
    private const string FileGlyph     = "";
    private const string CodeGlyph     = "";
    private const string ImageGlyph    = "";
    private const string MarkdownGlyph = "";
    private const string JsonGlyph     = "";
    private const string ConfigGlyph   = "";
    private const string ArchiveGlyph  = "";
    private const string PdfGlyph      = "";
    private const string TextGlyph     = "";


    /// <summary>
    /// Compact unit-suffixed size string for the default fs.ls table view.
    /// Directories render as a single dash since size has no meaningful value
    /// at the directory level without a recursive walk.
    /// </summary>
    private static string FormatSize(long bytes, bool isDir)
    {
        if (isDir) return "-";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return string.Format(CultureInfo.InvariantCulture, "{0:F1} KB", bytes / 1024.0);
        if (bytes < 1024L * 1024 * 1024) return string.Format(CultureInfo.InvariantCulture, "{0:F1} MB", bytes / (1024.0 * 1024));
        return string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", bytes / (1024.0 * 1024 * 1024));
    }
}
