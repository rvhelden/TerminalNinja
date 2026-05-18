using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class XmlModuleTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    private const string SimpleDoc =
        "<person id='1'><name>Alice</name><age>30</age></person>";

    private const string SiblingsDoc =
        "<root><item>1</item><item>2</item><item>3</item><other>x</other></root>";

    // ─── doc (parse) ────────────────────────────────────────────────────────

    [Test]
    public async Task XmlDoc_SystemKeysPresent()
    {
        var v = Run($"xml.doc(\"{SimpleDoc}\")");
        if (v is not NRecord rec) throw new InvalidOperationException();
        await Assert.That(rec.Fields["_name"]).IsEqualTo((NValue)new NString("person"));
        if (rec.Fields["_attrs"] is not NRecord attrs) throw new InvalidOperationException();
        await Assert.That(attrs.Fields["id"]).IsEqualTo((NValue)new NString("1"));
        if (rec.Fields["_children"] is not NList ch) throw new InvalidOperationException();
        await Assert.That(ch.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task XmlDoc_AutoMappedAccess_DotIntoUniqueChildren()
    {
        // Dot access through the auto-mapped key returns the single child record.
        var v = Run($"xml.doc(\"{SimpleDoc}\").name._text");
        await Assert.That(v).IsEqualTo((NValue)new NString("Alice"));
    }

    [Test]
    public async Task XmlDoc_SameNameSiblings_AutoMapToNList()
    {
        var v = Run($"xml.doc(\"{SiblingsDoc}\").item");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task XmlDoc_SingleChild_AutoMapToRecord()
    {
        var v = Run($"xml.doc(\"{SiblingsDoc}\").other");
        if (v is not NRecord rec) throw new InvalidOperationException();
        await Assert.That(rec.Fields["_text"]).IsEqualTo((NValue)new NString("x"));
    }

    [Test]
    public async Task XmlDoc_NoRoot_Throws()
    {
        // XmlDocument refuses to load an empty string.
        await Assert.That(() => Run("xml.doc(\"\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task XmlDoc_MalformedXml_Throws()
    {
        await Assert.That(() => Run("xml.doc(\"<unclosed>\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task XmlDoc_NonStringInput_Throws()
    {
        await Assert.That(() => Run("xml.doc(42)")).ThrowsExactly<EvaluatorException>();
    }

    // ─── save ───────────────────────────────────────────────────────────────

    [Test]
    public async Task XmlSave_RoundTripsParsedDoc()
    {
        // Round-trip a simple doc — save output should re-parse to an equivalent record.
        var v = Run(
            $"let doc = xml.doc(\"{SimpleDoc}\") in " +
            "let saved = xml.save(doc) in " +
            "xml.doc(saved)._name");
        await Assert.That(v).IsEqualTo((NValue)new NString("person"));
    }

    [Test]
    public async Task XmlSave_AcceptsHandBuiltRecord()
    {
        // The save side reads only the system keys — users can construct an
        // NRecord with just _name / _attrs / _children / _text and save it.
        var v = Run(
            "xml.save({ _name: \"greeting\", _attrs: { lang: \"en\" }, _text: \"hi\", _children: [] })");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value.Contains("<greeting")).IsTrue();
        await Assert.That(s.Value.Contains("lang=\"en\"")).IsTrue();
        await Assert.That(s.Value.Contains(">hi</greeting>")).IsTrue();
    }

    [Test]
    public async Task XmlSave_IndentOption_PrettyPrint()
    {
        var v = Run(
            $"xml.save(xml.doc(\"{SimpleDoc}\"), {{ indent: 2 }})");
        if (v is not NString s) throw new InvalidOperationException();
        // Pretty-printed output should have newlines.
        await Assert.That(s.Value.Contains("\n")).IsTrue();
    }

    [Test]
    public async Task XmlSave_DeclarationOption_AddsXmlHeader()
    {
        var v = Run(
            $"xml.save(xml.doc(\"{SimpleDoc}\"), {{ declaration: true }})");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value.StartsWith("<?xml")).IsTrue();
    }

    [Test]
    public async Task XmlSave_MissingSystemKeys_Throws()
    {
        await Assert.That(() => Run("xml.save({ })")).ThrowsExactly<EvaluatorException>();
    }

    // ─── helpers ────────────────────────────────────────────────────────────

    [Test]
    public async Task XmlText_ConcatenatesRecursiveText()
    {
        var v = Run("xml.text(xml.doc(\"<p>hello <em>world</em>!</p>\"))");
        if (v is not NString s) throw new InvalidOperationException();
        // Direct text "hello !" + em's text "world" (XmlDocument may trim/collapse whitespace).
        await Assert.That(s.Value.Contains("hello")).IsTrue();
        await Assert.That(s.Value.Contains("world")).IsTrue();
    }

    [Test]
    public async Task XmlAttr_ExistingAttr_ReturnsValue()
    {
        var v = Run($"xml.attr(xml.doc(\"{SimpleDoc}\"), \"id\")");
        await Assert.That(v).IsEqualTo((NValue)new NString("1"));
    }

    [Test]
    public async Task XmlAttr_MissingAttr_NoDefault_Throws()
    {
        await Assert.That(() => Run($"xml.attr(xml.doc(\"{SimpleDoc}\"), \"missing\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task XmlAttr_MissingAttr_WithDefault_ReturnsDefault()
    {
        var v = Run($"xml.attr(xml.doc(\"{SimpleDoc}\"), \"missing\", \"fallback\")");
        await Assert.That(v).IsEqualTo((NValue)new NString("fallback"));
    }

    [Test]
    public async Task XmlFind_DirectChild_ReturnsFirst()
    {
        var v = Run($"xml.find(xml.doc(\"{SiblingsDoc}\"), \"item\")._text");
        await Assert.That(v).IsEqualTo((NValue)new NString("1"));
    }

    [Test]
    public async Task XmlFind_NonExistent_ReturnsUnit()
    {
        var v = Run($"xml.find(xml.doc(\"{SimpleDoc}\"), \"absent\")");
        await Assert.That(v is NUnit).IsTrue();
    }

    [Test]
    public async Task XmlFindAll_ReturnsAllMatchingDirectChildren()
    {
        var v = Run($"xml.find_all(xml.doc(\"{SiblingsDoc}\"), \"item\")");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task XmlFindAll_NoMatches_ReturnsEmptyList()
    {
        var v = Run($"xml.find_all(xml.doc(\"{SimpleDoc}\"), \"absent\")");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(0);
    }

    // ─── xpath ──────────────────────────────────────────────────────────────

    [Test]
    public async Task XmlXpath_SelectElements_ReturnsRecords()
    {
        var v = Run($"xml.xpath(xml.doc(\"{SiblingsDoc}\"), \"//item\")");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task XmlXpath_SelectAttributes_ReturnsStrings()
    {
        var v = Run($"xml.xpath(xml.doc(\"{SimpleDoc}\"), \"//@id\")");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(1);
        await Assert.That(list.Items[0]).IsEqualTo((NValue)new NString("1"));
    }

    [Test]
    public async Task XmlXpath_FilteredQuery_Works()
    {
        var v = Run($"xml.xpath(xml.doc(\"{SiblingsDoc}\"), \"//item[position()<=2]\")");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task XmlXpath_InvalidExpression_Throws()
    {
        // Use a literal `[` to trigger XPath parse failure in the BCL.
        await Assert.That(() => Run($"xml.xpath(xml.doc(\"{SimpleDoc}\"), \"//[bad-syntax\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task XmlXpath_NoMatches_ReturnsEmptyList()
    {
        var v = Run($"xml.xpath(xml.doc(\"{SimpleDoc}\"), \"//nothing\")");
        if (v is not NList list) throw new InvalidOperationException();
        await Assert.That(list.Items.Length).IsEqualTo(0);
    }
}
