using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class FsModuleTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    private static string MakeSandbox()
    {
        var path = Path.Combine(Path.GetTempPath(), "ninja_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Escape(string p) => p.Replace("\\", "\\\\");

    [Test]
    public async Task FsPwd_ReturnsCurrentDirectory()
    {
        var v = Run("fs.pwd()");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).IsEqualTo(Directory.GetCurrentDirectory());
    }

    [Test]
    public async Task FsCd_ChangesCwd_AndReturnsNewPath()
    {
        var sandbox = MakeSandbox();
        var original = Directory.GetCurrentDirectory();
        try
        {
            var v = Run($"fs.cd(\"{Escape(sandbox)}\")");
            if (v is not NString s) throw new InvalidOperationException();
            await Assert.That(Path.GetFullPath(s.Value)).IsEqualTo(Path.GetFullPath(sandbox));
            await Assert.That(Path.GetFullPath(Directory.GetCurrentDirectory())).IsEqualTo(Path.GetFullPath(sandbox));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Test]
    public async Task FsExists_TrueForFileAndDir_FalseForMissing()
    {
        var sandbox = MakeSandbox();
        try
        {
            var file = Path.Combine(sandbox, "f.txt");
            File.WriteAllText(file, "x");
            var missing = Path.Combine(sandbox, "nope");
            await Assert.That(Run($"fs.exists(\"{Escape(sandbox)}\")")).IsEqualTo((NValue)new NBool(true));
            await Assert.That(Run($"fs.exists(\"{Escape(file)}\")")).IsEqualTo((NValue)new NBool(true));
            await Assert.That(Run($"fs.exists(\"{Escape(missing)}\")")).IsEqualTo((NValue)new NBool(false));
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsIsDir_TrueOnlyForDirectory()
    {
        var sandbox = MakeSandbox();
        try
        {
            var file = Path.Combine(sandbox, "f.txt");
            File.WriteAllText(file, "x");
            await Assert.That(Run($"fs.is_dir(\"{Escape(sandbox)}\")")).IsEqualTo((NValue)new NBool(true));
            await Assert.That(Run($"fs.is_dir(\"{Escape(file)}\")")).IsEqualTo((NValue)new NBool(false));
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsWriteThenRead_RoundTripsContent()
    {
        var sandbox = MakeSandbox();
        try
        {
            var file = Path.Combine(sandbox, "out.txt");
            await Assert.That(Run($"fs.write(\"{Escape(file)}\", \"hello\\nworld\")") is NUnit).IsTrue();
            await Assert.That(Run($"fs.read(\"{Escape(file)}\")"))
                .IsEqualTo((NValue)new NString("hello\nworld"));
            await Assert.That(Run($"fs.cat(\"{Escape(file)}\")"))
                .IsEqualTo((NValue)new NString("hello\nworld"));
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsAppend_AddsToEnd()
    {
        var sandbox = MakeSandbox();
        try
        {
            var file = Path.Combine(sandbox, "log.txt");
            Run($"fs.write(\"{Escape(file)}\", \"a\")");
            Run($"fs.append(\"{Escape(file)}\", \"b\")");
            await Assert.That(File.ReadAllText(file)).IsEqualTo("ab");
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsLs_ReturnsRecordsWithExpectedKeys()
    {
        var sandbox = MakeSandbox();
        try
        {
            File.WriteAllText(Path.Combine(sandbox, "a.txt"), "hello");
            Directory.CreateDirectory(Path.Combine(sandbox, "sub"));

            var v = Run($"fs.ls(\"{Escape(sandbox)}\")");
            if (v is not NList list) throw new InvalidOperationException();
            await Assert.That(list.Items.Length).IsEqualTo(2);
            foreach (var item in list.Items)
            {
                if (item is not NRecord rec) throw new InvalidOperationException("ls entry not a record");
                await Assert.That(rec.Fields.ContainsKey("Name")).IsTrue();
                await Assert.That(rec.Fields.ContainsKey("FullPath")).IsTrue();
                await Assert.That(rec.Fields.ContainsKey("IsDirectory")).IsTrue();
                await Assert.That(rec.Fields.ContainsKey("Size")).IsTrue();
                await Assert.That(rec.Fields.ContainsKey("LastModified")).IsTrue();
            }
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsLs_RecurseOption_PicksUpNested()
    {
        var sandbox = MakeSandbox();
        try
        {
            Directory.CreateDirectory(Path.Combine(sandbox, "sub"));
            File.WriteAllText(Path.Combine(sandbox, "sub", "deep.txt"), "x");

            var topOnly = Run($"fs.ls(\"{Escape(sandbox)}\") | count");
            var recursed = Run($"fs.ls(\"{Escape(sandbox)}\", {{ recurse: true }}) | count");
            // top: sub/ ; recurse: sub/ + sub/deep.txt
            await Assert.That(topOnly).IsEqualTo((NValue)new NInt(1));
            await Assert.That(recursed).IsEqualTo((NValue)new NInt(2));
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsLs_PatternOption_FiltersEntries()
    {
        var sandbox = MakeSandbox();
        try
        {
            File.WriteAllText(Path.Combine(sandbox, "a.txt"), "x");
            File.WriteAllText(Path.Combine(sandbox, "b.log"), "x");
            File.WriteAllText(Path.Combine(sandbox, "c.txt"), "x");

            var v = Run($"fs.ls(\"{Escape(sandbox)}\", {{ pattern: \"*.txt\" }}) | count");
            await Assert.That(v).IsEqualTo((NValue)new NInt(2));
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsMkdir_CreatesDirectory()
    {
        var sandbox = MakeSandbox();
        try
        {
            var nested = Path.Combine(sandbox, "newdir");
            Run($"fs.mkdir(\"{Escape(nested)}\")");
            await Assert.That(Directory.Exists(nested)).IsTrue();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsMkdir_NoRecursive_FailsIfParentMissing()
    {
        var sandbox = MakeSandbox();
        try
        {
            var deep = Path.Combine(sandbox, "missing", "nested");
            await Assert.That(() => Run($"fs.mkdir(\"{Escape(deep)}\")"))
                .ThrowsExactly<EvaluatorException>();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsMkdir_RecursiveOption_CreatesIntermediateDirs()
    {
        var sandbox = MakeSandbox();
        try
        {
            var deep = Path.Combine(sandbox, "a", "b", "c");
            Run($"fs.mkdir(\"{Escape(deep)}\", {{ recursive: true }})");
            await Assert.That(Directory.Exists(deep)).IsTrue();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsRm_RemovesFile()
    {
        var sandbox = MakeSandbox();
        try
        {
            var file = Path.Combine(sandbox, "doomed.txt");
            File.WriteAllText(file, "x");
            Run($"fs.rm(\"{Escape(file)}\")");
            await Assert.That(File.Exists(file)).IsFalse();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsRm_OnDirectoryWithoutRecursive_Throws()
    {
        var sandbox = MakeSandbox();
        try
        {
            var sub = Path.Combine(sandbox, "sub");
            Directory.CreateDirectory(sub);
            await Assert.That(() => Run($"fs.rm(\"{Escape(sub)}\")"))
                .ThrowsExactly<EvaluatorException>();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsRm_RecursiveOption_RemovesDirectory()
    {
        var sandbox = MakeSandbox();
        try
        {
            var sub = Path.Combine(sandbox, "sub");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, "f"), "x");
            Run($"fs.rm(\"{Escape(sub)}\", {{ recursive: true }})");
            await Assert.That(Directory.Exists(sub)).IsFalse();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsRm_MissingPath_ForceTrue_NoOps()
    {
        var sandbox = MakeSandbox();
        try
        {
            var missing = Path.Combine(sandbox, "nope");
            var v = Run($"fs.rm(\"{Escape(missing)}\", {{ force: true }})");
            await Assert.That(v is NUnit).IsTrue();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsRm_MissingPath_WithoutForce_Throws()
    {
        var sandbox = MakeSandbox();
        try
        {
            var missing = Path.Combine(sandbox, "nope");
            await Assert.That(() => Run($"fs.rm(\"{Escape(missing)}\")"))
                .ThrowsExactly<EvaluatorException>();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsMove_RenamesFile()
    {
        var sandbox = MakeSandbox();
        try
        {
            var src = Path.Combine(sandbox, "a.txt");
            var dst = Path.Combine(sandbox, "b.txt");
            File.WriteAllText(src, "hello");
            Run($"fs.move(\"{Escape(src)}\", \"{Escape(dst)}\")");
            await Assert.That(File.Exists(dst)).IsTrue();
            await Assert.That(File.Exists(src)).IsFalse();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsCopy_DuplicatesFileContents()
    {
        var sandbox = MakeSandbox();
        try
        {
            var src = Path.Combine(sandbox, "a.txt");
            var dst = Path.Combine(sandbox, "b.txt");
            File.WriteAllText(src, "hello");
            Run($"fs.copy(\"{Escape(src)}\", \"{Escape(dst)}\")");
            await Assert.That(File.ReadAllText(dst)).IsEqualTo("hello");
            await Assert.That(File.Exists(src)).IsTrue();
        }
        finally { Directory.Delete(sandbox, recursive: true); }
    }

    [Test]
    public async Task FsCat_NotFound_Throws()
    {
        await Assert.That(() => Run("fs.cat(\"non-existent-file-xyz\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task FlatLsRemovedAfterMigration_IsUnboundName()
    {
        // The flat ls / cd / pwd / cat builtins are removed in this commit.
        await Assert.That(() => Run("ls()")).ThrowsExactly<EvaluatorException>();
        await Assert.That(() => Run("pwd()")).ThrowsExactly<EvaluatorException>();
        await Assert.That(() => Run("cd(\".\")")).ThrowsExactly<EvaluatorException>();
        await Assert.That(() => Run("cat(\"x\")")).ThrowsExactly<EvaluatorException>();
    }
}
