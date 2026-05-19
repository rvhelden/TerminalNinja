using System.Collections.Immutable;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Language.Tests.Unit;

/// <summary>
/// Tests for <see cref="ValueFormatter.StripHiddenFields"/>, the hover-panel
/// filter that removes record keys starting with <c>__</c>.
/// </summary>
public class ValueFormatterStripHiddenFieldsTests
{
    private static NRecord Rec(params (string key, NValue value)[] fields)
    {
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var (k, v) in fields) b[k] = v;
        return new NRecord(b.ToImmutable());
    }

    [Test]
    public async Task TopLevelDoubleUnderscoreKeys_AreRemoved()
    {
        var rec = Rec(
            ("__type", new NString("user")),
            ("name", new NString("ronald")),
            ("__src", new NString("local")));
        var stripped = ValueFormatter.StripHiddenFields(rec);
        if (stripped is not NRecord r) throw new InvalidOperationException();
        await Assert.That(r.Fields.ContainsKey("__type")).IsFalse();
        await Assert.That(r.Fields.ContainsKey("__src")).IsFalse();
        await Assert.That(r.Fields.ContainsKey("name")).IsTrue();
    }

    [Test]
    public async Task SingleUnderscoreKeys_ArePreserved()
    {
        // Only the __ double-underscore convention is hidden; _foo and __ alone-prefix-letter are different cases.
        var rec = Rec(
            ("_kept", new NString("yes")),
            ("__hidden", new NString("no")));
        var stripped = ValueFormatter.StripHiddenFields(rec);
        if (stripped is not NRecord r) throw new InvalidOperationException();
        await Assert.That(r.Fields.ContainsKey("_kept")).IsTrue();
        await Assert.That(r.Fields.ContainsKey("__hidden")).IsFalse();
    }

    [Test]
    public async Task NestedRecordsInsideRecord_AreStripped()
    {
        var nested = Rec(
            ("__deep", new NInt(1)),
            ("kept", new NInt(2)));
        var outer = Rec(
            ("__top", new NString("hide")),
            ("inner", nested));
        var stripped = ValueFormatter.StripHiddenFields(outer);
        if (stripped is not NRecord r) throw new InvalidOperationException();
        await Assert.That(r.Fields.ContainsKey("__top")).IsFalse();
        if (r.Fields["inner"] is not NRecord ir) throw new InvalidOperationException();
        await Assert.That(ir.Fields.ContainsKey("__deep")).IsFalse();
        await Assert.That(ir.Fields.ContainsKey("kept")).IsTrue();
    }

    [Test]
    public async Task RecordsInsideList_AreStripped()
    {
        var list = new NList(ImmutableArray.Create<NValue>(
            Rec(("__x", new NInt(1)), ("y", new NInt(2))),
            Rec(("__a", new NInt(3)), ("b", new NInt(4)))));
        var stripped = ValueFormatter.StripHiddenFields(list);
        if (stripped is not NList nl) throw new InvalidOperationException();
        if (nl.Items[0] is not NRecord r0 || nl.Items[1] is not NRecord r1)
            throw new InvalidOperationException();
        await Assert.That(r0.Fields.ContainsKey("__x")).IsFalse();
        await Assert.That(r0.Fields.ContainsKey("y")).IsTrue();
        await Assert.That(r1.Fields.ContainsKey("__a")).IsFalse();
        await Assert.That(r1.Fields.ContainsKey("b")).IsTrue();
    }

    [Test]
    public async Task Scalars_PassThroughUnchanged()
    {
        var stripped = ValueFormatter.StripHiddenFields(new NString("hello"));
        if (stripped is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task DumpAfterStripping_OmitsHiddenKeys()
    {
        var rec = Rec(
            ("__type", new NString("user")),
            ("name", new NString("ronald")));
        var dump = ValueFormatter.Dump(ValueFormatter.StripHiddenFields(rec));
        await Assert.That(dump.Contains("__type")).IsFalse();
        await Assert.That(dump.Contains("name")).IsTrue();
    }
}
