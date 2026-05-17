using System.Collections.Immutable;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>Minimal filesystem builtins: <c>pwd</c>, <c>cd</c>, <c>ls</c>, <c>cat</c>.</summary>
public static class Fs
{
    /// <summary>Register filesystem builtins into <paramref name="b"/>.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        b["pwd"] = new NFunc(args =>
        {
            if (args.Length != 0) throw new EvaluatorException($"pwd expects 0 arguments, got {args.Length}");
            return new NString(Directory.GetCurrentDirectory());
        }, 0);

        b["cd"] = new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"cd expects 1 argument, got {args.Length}");
            if (args[0] is not NString s) throw new EvaluatorException("cd expects a string path");
            var path = Path.GetFullPath(s.Value);
            if (!Directory.Exists(path)) throw new EvaluatorException($"cd: directory not found: {path}");
            Directory.SetCurrentDirectory(path);
            return new NString(Directory.GetCurrentDirectory());
        }, 1);

        b["ls"] = new NFunc(args =>
        {
            string target = Directory.GetCurrentDirectory();
            if (args.Length == 1)
            {
                if (args[0] is not NString s) throw new EvaluatorException("ls expects a string path or no argument");
                target = Path.GetFullPath(s.Value);
            }
            else if (args.Length > 1)
            {
                throw new EvaluatorException($"ls expects 0 or 1 argument, got {args.Length}");
            }
            if (!Directory.Exists(target)) throw new EvaluatorException($"ls: directory not found: {target}");

            var b2 = ImmutableArray.CreateBuilder<NValue>();
            foreach (var dir in Directory.EnumerateDirectories(target))
            {
                b2.Add(MakeEntry(dir, isDir: true, size: 0));
            }
            foreach (var file in Directory.EnumerateFiles(target))
            {
                long size = 0;
                try { size = new FileInfo(file).Length; } catch { /* unreadable — surface as 0 */ }
                b2.Add(MakeEntry(file, isDir: false, size: size));
            }
            return new NList(b2.ToImmutable());
        }, -1);

        b["cat"] = new NFunc(args =>
        {
            if (args.Length != 1) throw new EvaluatorException($"cat expects 1 argument, got {args.Length}");
            if (args[0] is not NString s) throw new EvaluatorException("cat expects a string path");
            var path = Path.GetFullPath(s.Value);
            if (!File.Exists(path)) throw new EvaluatorException($"cat: file not found: {path}");
            return new NString(File.ReadAllText(path));
        }, 1);
    }

    private static NValue MakeEntry(string fullPath, bool isDir, long size)
    {
        var d = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        d["Name"] = new NString(Path.GetFileName(fullPath));
        d["IsDirectory"] = new NBool(isDir);
        d["Size"] = new NInt(size);
        return new NRecord(d.ToImmutable());
    }
}
