using System.Collections.Immutable;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>xml</c> module — parse XML into an auto-mapped record form, serialise
/// back to a string, and query with XPath. Uses <see cref="System.Xml.XmlDocument"/>
/// + <see cref="XmlWriter"/> (BCL, AOT-safe — no reflection).
/// </summary>
/// <remarks>
/// Element record shape:
/// <code>
/// {
///   _name:     "person",
///   _attrs:    { id: "1" },
///   _text:     "concatenated direct text",
///   _children: [&lt;child&gt;, ...],          // ordered, all element children
///   &lt;child_name&gt;: &lt;child&gt; or NList of &lt;child&gt;,   // auto-mapped: single record when one,
///                                              // NList when multiple same-name siblings
/// }
/// </code>
/// <para>
/// Save reads only the system keys (<c>_name</c>, <c>_attrs</c>, <c>_text</c>,
/// <c>_children</c>); the auto-mapped accessors are pure-read convenience
/// regenerated on every parse, and may be safely absent on hand-built records.
/// </para>
/// </remarks>
public static class XmlModule
{
    /// <summary>Register the <c>xml</c> module.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        BuiltinRegistry.RegisterModule(b, "xml",
            ("doc", new NFunc(Doc, 1)),
            ("save", new NFunc(Save, -1)),
            ("text", new NFunc(Text, 1)),
            ("attr", new NFunc(Attr, -1)),
            ("find", new NFunc(Find, 2)),
            ("find_all", new NFunc(FindAll, 2)),
            ("xpath", new NFunc(Xpath, 2)));
    }

    // ─── doc (parse) ────────────────────────────────────────────────────────

    private static NValue Doc(NValue[] args)
    {
        if (args.Length != 1) throw new EvaluatorException($"xml.doc expects 1 argument, got {args.Length}");
        if (args[0] is not NString s) throw new EvaluatorException("xml.doc: input must be a string");

        var doc = new XmlDocument();
        try { doc.LoadXml(s.Value); }
        catch (XmlException ex) { throw new EvaluatorException($"xml.doc: parse error: {ex.Message}", ex); }

        if (doc.DocumentElement is null) throw new EvaluatorException("xml.doc: document has no root element");
        return ElementToRecord(doc.DocumentElement);
    }

    private static NValue ElementToRecord(XmlElement elem)
    {
        var attrs = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        if (elem.Attributes is not null)
        {
            foreach (XmlAttribute a in elem.Attributes)
                attrs[a.Name] = new NString(a.Value);
        }

        var children = ImmutableArray.CreateBuilder<NValue>();
        var textBuf = new StringBuilder();
        foreach (XmlNode child in elem.ChildNodes)
        {
            switch (child)
            {
                case XmlElement childElem:
                    children.Add(ElementToRecord(childElem));
                    break;
                case XmlCDataSection cdata:
                    textBuf.Append(cdata.Value);
                    break;
                case XmlText t:
                    textBuf.Append(t.Value);
                    break;
                case XmlSignificantWhitespace sw:
                    textBuf.Append(sw.Value);
                    break;
                // Comments, processing instructions, whitespace are dropped.
            }
        }

        // Group element children by name for auto-mapping.
        var grouped = new Dictionary<string, List<NValue>>(StringComparer.Ordinal);
        var orderedNames = new List<string>();
        foreach (var c in children)
        {
            if (c is not NRecord cr) continue;
            if (!cr.Fields.TryGetValue("_name", out var nameVal) || nameVal is not NString ns) continue;
            if (!grouped.TryGetValue(ns.Value, out var list))
            {
                list = new List<NValue>();
                grouped[ns.Value] = list;
                orderedNames.Add(ns.Value);
            }
            list.Add(c);
        }

        var rec = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        // Auto-mapped accessors first — system keys overwrite collisions.
        foreach (var name in orderedNames)
        {
            var grp = grouped[name];
            rec[name] = grp.Count == 1
                ? grp[0]
                : new NList(ImmutableArray.CreateRange(grp));
        }
        // System keys.
        rec["_name"] = new NString(elem.Name);
        rec["_attrs"] = new NRecord(attrs.ToImmutable());
        rec["_text"] = new NString(textBuf.ToString().Trim());
        rec["_children"] = new NList(children.ToImmutable());
        return new NRecord(rec.ToImmutable());
    }

    // ─── save ───────────────────────────────────────────────────────────────

    private static NValue Save(NValue[] args)
    {
        if (args.Length is < 1 or > 2)
            throw new EvaluatorException($"xml.save expects 1 or 2 arguments, got {args.Length}");
        if (args[0] is not NRecord rec) throw new EvaluatorException("xml.save: input must be a record");

        int indent = 0;
        bool declaration = false;
        if (args.Length == 2)
        {
            if (args[1] is not NRecord opts) throw new EvaluatorException("xml.save: options must be a record");
            if (opts.Fields.TryGetValue("indent", out var iv))
            {
                if (iv is not NInt ni) throw new EvaluatorException("xml.save: 'indent' must be an int");
                if (ni.Value < 0) throw new EvaluatorException("xml.save: 'indent' must be non-negative");
                indent = (int)Math.Min(ni.Value, 32);
            }
            if (opts.Fields.TryGetValue("declaration", out var dv))
            {
                if (dv is not NBool db) throw new EvaluatorException("xml.save: 'declaration' must be a bool");
                declaration = db.Value;
            }
        }

        return new NString(WriteToString(rec, indent, declaration));
    }

    private static string WriteToString(NRecord rec, int indent, bool declaration)
    {
        var settings = new XmlWriterSettings
        {
            Indent = indent > 0,
            IndentChars = new string(' ', Math.Max(indent, 0)),
            OmitXmlDeclaration = !declaration,
            Encoding = Encoding.UTF8,
        };
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        using (var writer = XmlWriter.Create(sw, settings))
        {
            WriteElement(writer, rec);
        }
        return sb.ToString();
    }

    private static void WriteElement(XmlWriter w, NRecord elem)
    {
        if (!elem.Fields.TryGetValue("_name", out var nameVal) || nameVal is not NString name)
            throw new EvaluatorException("xml.save: element record missing '_name' (NString)");

        w.WriteStartElement(name.Value);

        if (elem.Fields.TryGetValue("_attrs", out var attrsVal))
        {
            if (attrsVal is not NRecord attrs) throw new EvaluatorException("xml.save: '_attrs' must be a record");
            foreach (var kv in attrs.Fields)
            {
                if (kv.Value is not NString av) throw new EvaluatorException($"xml.save: attribute '{kv.Key}' must be a string");
                w.WriteAttributeString(kv.Key, av.Value);
            }
        }

        if (elem.Fields.TryGetValue("_text", out var textVal) && textVal is NString text && text.Value.Length > 0)
        {
            w.WriteString(text.Value);
        }

        if (elem.Fields.TryGetValue("_children", out var childrenVal))
        {
            if (childrenVal is not NList childrenList) throw new EvaluatorException("xml.save: '_children' must be a list");
            foreach (var child in childrenList.Items)
            {
                if (child is not NRecord childRec) throw new EvaluatorException("xml.save: every child in '_children' must be a record");
                WriteElement(w, childRec);
            }
        }

        w.WriteEndElement();
    }

    // ─── helpers (text / attr / find / find_all) ────────────────────────────

    private static NValue Text(NValue[] args)
    {
        if (args.Length != 1) throw new EvaluatorException($"xml.text expects 1 argument, got {args.Length}");
        if (args[0] is not NRecord elem) throw new EvaluatorException("xml.text: input must be a record");
        var sb = new StringBuilder();
        CollectText(elem, sb);
        return new NString(sb.ToString());
    }

    private static void CollectText(NRecord elem, StringBuilder sb)
    {
        if (elem.Fields.TryGetValue("_text", out var t) && t is NString ts) sb.Append(ts.Value);
        if (elem.Fields.TryGetValue("_children", out var c) && c is NList childList)
        {
            foreach (var child in childList.Items)
                if (child is NRecord cr) CollectText(cr, sb);
        }
    }

    private static NValue Attr(NValue[] args)
    {
        if (args.Length is < 2 or > 3) throw new EvaluatorException($"xml.attr expects 2 or 3 arguments, got {args.Length}");
        if (args[0] is not NRecord elem) throw new EvaluatorException("xml.attr: input must be a record");
        if (args[1] is not NString name) throw new EvaluatorException("xml.attr: name must be a string");
        if (!elem.Fields.TryGetValue("_attrs", out var attrsVal) || attrsVal is not NRecord attrs)
            throw new EvaluatorException("xml.attr: element has no '_attrs' record");
        if (attrs.Fields.TryGetValue(name.Value, out var v)) return v;
        if (args.Length == 3) return args[2];
        throw new EvaluatorException($"xml.attr: attribute '{name.Value}' not found");
    }

    private static NValue Find(NValue[] args)
    {
        if (args.Length != 2) throw new EvaluatorException($"xml.find expects 2 arguments, got {args.Length}");
        if (args[0] is not NRecord elem) throw new EvaluatorException("xml.find: input must be a record");
        if (args[1] is not NString name) throw new EvaluatorException("xml.find: name must be a string");
        if (!elem.Fields.TryGetValue("_children", out var c) || c is not NList childList)
            return NUnit.Instance;
        foreach (var child in childList.Items)
        {
            if (child is NRecord cr
                && cr.Fields.TryGetValue("_name", out var n)
                && n is NString ns
                && ns.Value == name.Value)
                return cr;
        }
        return NUnit.Instance;
    }

    private static NValue FindAll(NValue[] args)
    {
        if (args.Length != 2) throw new EvaluatorException($"xml.find_all expects 2 arguments, got {args.Length}");
        if (args[0] is not NRecord elem) throw new EvaluatorException("xml.find_all: input must be a record");
        if (args[1] is not NString name) throw new EvaluatorException("xml.find_all: name must be a string");
        if (!elem.Fields.TryGetValue("_children", out var c) || c is not NList childList)
            return new NList(ImmutableArray<NValue>.Empty);

        var b = ImmutableArray.CreateBuilder<NValue>();
        foreach (var child in childList.Items)
        {
            if (child is NRecord cr
                && cr.Fields.TryGetValue("_name", out var n)
                && n is NString ns
                && ns.Value == name.Value)
                b.Add(cr);
        }
        return new NList(b.ToImmutable());
    }

    // ─── xpath ──────────────────────────────────────────────────────────────

    private static NValue Xpath(NValue[] args)
    {
        if (args.Length != 2) throw new EvaluatorException($"xml.xpath expects 2 arguments, got {args.Length}");
        if (args[0] is not NRecord elem) throw new EvaluatorException("xml.xpath: input must be a record");
        if (args[1] is not NString expr) throw new EvaluatorException("xml.xpath: expression must be a string");

        // Round-trip the element through a fresh XmlDocument so the BCL handles
        // XPath evaluation. MVP-acceptable cost: each call re-serialises + re-parses;
        // optimise later if hot-path use shows up.
        string serialised = WriteToString(elem, indent: 0, declaration: false);
        var doc = new XmlDocument();
        try { doc.LoadXml(serialised); }
        catch (XmlException ex) { throw new EvaluatorException($"xml.xpath: internal round-trip parse failed: {ex.Message}", ex); }

        XmlNodeList? matches;
        try { matches = doc.DocumentElement?.SelectNodes(expr.Value); }
        catch (XPathException ex) { throw new EvaluatorException($"xml.xpath: invalid expression '{expr.Value}': {ex.Message}", ex); }
        if (matches is null) return new NList(ImmutableArray<NValue>.Empty);

        var results = ImmutableArray.CreateBuilder<NValue>();
        foreach (XmlNode node in matches)
        {
            results.Add(node switch
            {
                XmlElement e => ElementToRecord(e),
                XmlAttribute a => new NString(a.Value ?? string.Empty),
                XmlCDataSection c => new NString(c.Value ?? string.Empty),
                XmlText t => new NString(t.Value ?? string.Empty),
                _ => new NString(node.OuterXml),
            });
        }
        return new NList(results.ToImmutable());
    }
}
