using System.Collections.ObjectModel;
using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Every item bound into an <see cref="ItemsControl"/> must produce exactly one visible row.
/// </summary>
/// <remarks>
/// A row that is simply absent is the worst kind of layout bug: nothing is clipped, nothing
/// overlaps, the rows below just shift up by one and the list reads as complete. These tests
/// assert the invariant positionally — row <c>i</c> of the source collection must be the text
/// drawn on line <c>i</c> of the panel — so a dropped child is caught by the shift it causes
/// rather than by a missing substring that a neighbour might coincidentally supply.
/// </remarks>
public class ItemsControlRowVisibilityTests
{
    internal sealed class TextRow : ViewModelBase
    {
        private string _text = "";
        private Color _colour = Color.White;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public Color Colour
        {
            get => _colour;
            set => SetProperty(ref _colour, value);
        }
    }

    internal sealed class RowsViewModel : ViewModelBase
    {
        public ObservableCollection<TextRow> Lines { get; } = [];
    }

    /// <summary>The shortcut dialog's own layout: a templated ItemsControl inside a double border.</summary>
    private const string Layout = """
        <Window xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                Title="shortcuts">
            <Window.Resources>
                <DataTemplate x:Key="TextRowTemplate">
                    <TextBlock Text="{Binding Text}" Foreground="{Binding Colour}" Padding="1,0,0,0" />
                </DataTemplate>
            </Window.Resources>
            <Border BorderStyle="Double">
                <ItemsControl ItemsSource="{Binding Lines}" ItemTemplate="{StaticResource TextRowTemplate}" />
            </Border>
        </Window>
        """;

    /// <summary>The same list with no border, so a captured line maps straight onto a row.</summary>
    private const string BarePanelLayout = """
        <Window xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Window.Resources>
                <DataTemplate x:Key="TextRowTemplate">
                    <TextBlock Text="{Binding Text}" Foreground="{Binding Colour}" Padding="1,0,0,0" />
                </DataTemplate>
            </Window.Resources>
            <ItemsControl ItemsSource="{Binding Lines}" ItemTemplate="{StaticResource TextRowTemplate}" />
        </Window>
        """;

    // ─── Harness ─────────────────────────────────────────────────────

    /// <summary>
    /// Renders <paramref name="rows"/> through the given layout and returns the captured frame
    /// as raw lines, untrimmed, so a row's column position is preserved.
    /// </summary>
    private static string[] Capture(
        IReadOnlyList<string> rows,
        string layout,
        int width,
        int height,
        bool addAfterLoad = false)
    {
        var vm = new RowsViewModel();

        if (!addAfterLoad)
        {
            foreach (var row in rows)
            {
                vm.Lines.Add(new TextRow { Text = row });
            }
        }

        var window = TerminalXaml.Load<Window>(layout, vm);

        if (addAfterLoad)
        {
            foreach (var row in rows)
            {
                vm.Lines.Add(new TextRow { Text = row });
            }
        }

        var buffer = new CellBuffer(width, height);
        window.Render(buffer, new Rect(0, 0, width, height));

        var lines = new string[height];
        for (var y = 0; y < height; y++)
        {
            var line = new StringBuilder(width);
            for (var x = 0; x < width; x++)
            {
                var codepoint = buffer[x, y].Codepoint;

                // TextBlock's plain-text path writes UTF-16 code units, so a surrogate half can
                // legitimately be sitting in a cell; ConvertFromUtf32 rejects those.
                line.Append(codepoint switch
                {
                    0 => " ",
                    >= 0xD800 and <= 0xDFFF => ((char)codepoint).ToString(),
                    _ => char.ConvertFromUtf32((int)codepoint),
                });
            }

            lines[y] = line.ToString();
        }

        return lines;
    }

    /// <summary>
    /// Returns the index of the first row that did not land on its own line, or -1 when every
    /// row is present and in order. <paramref name="left"/> is the column the text starts at.
    /// </summary>
    private static int FirstMissingRow(IReadOnlyList<string> rows, string[] lines, int top, int left, int room)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var y = top + i;
            if (y >= lines.Length)
            {
                break; // out of the capture; not this test's business
            }

            var expected = rows[i];
            if (expected.Length > room)
            {
                expected = expected[..room];
            }

            var actual = lines[y].Substring(left, Math.Min(room, lines[y].Length - left));

            // A row's own trailing spaces are indistinguishable from untouched cells.
            if (!actual.TrimEnd().Equals(expected.TrimEnd(), StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // ─── The reported case: the shortcut dialog ──────────────────────

    [Test]
    public async Task ShortcutDialog_EveryRowIsRendered()
    {
        var rows = HelpRows();
        var lines = Capture(rows, Layout, 68, 38);

        // Border 1 + TextBlock padding 1.
        var missing = FirstMissingRow(rows, lines, top: 1, left: 2, room: 68 - 3);

        await Assert.That(missing).IsEqualTo(-1);
    }

    [Test]
    [Arguments(60)]
    [Arguments(66)]
    [Arguments(68)]
    [Arguments(72)]
    [Arguments(80)]
    [Arguments(140)]
    public async Task ShortcutDialog_EveryRowIsRendered_AtAnyWidth(int width)
    {
        var rows = HelpRows();
        var lines = Capture(rows, Layout, width, 44);
        var missing = FirstMissingRow(rows, lines, top: 1, left: 2, room: width - 3);

        await Assert.That(missing).IsEqualTo(-1);
    }

    [Test]
    public async Task ShortcutDialog_EveryRowIsRendered_WhenRowsArriveAfterBinding()
    {
        var rows = HelpRows();
        var lines = Capture(rows, Layout, 68, 38, addAfterLoad: true);
        var missing = FirstMissingRow(rows, lines, top: 1, left: 2, room: 65);

        await Assert.That(missing).IsEqualTo(-1);
    }

    // ─── Fuzz: content, count and height ─────────────────────────────

    /// <summary>
    /// Characters worth suspecting: the em dash the real strings use, the punctuation that
    /// distinguished the row that vanished from the ones that did not, and the width and
    /// normalisation hazards a cell grid is sensitive to.
    /// </summary>
    private static readonly string[] Alphabet =
    [
        "a", "z", " ", "  ", "—", ":", ";", ",", ".", "/", "'", "-", "?", "!",
        " ",   // no-break space
        "​",   // zero-width space
        "­",   // soft hyphen
        "́",   // combining acute
        "…",   // ellipsis
        "│",   // box drawing
        "漢",        // wide
        "가",        // wide
        "🚀", // wide, surrogate pair
        "A", "Q",
    ];

    /// <summary>
    /// Renders thousands of random lists and asserts only that each row occupies its own line,
    /// by stamping every row with a unique ASCII marker that always fits inside the width. The
    /// marker, not the payload, is what is asserted: how an exotic codepoint is *drawn* is a
    /// separate question from whether its row exists at all, and mixing the two hides the second.
    /// </summary>
    [Test]
    public async Task Fuzz_EveryBoundRowOccupiesItsOwnLine()
    {
        var random = new Random(31);
        var failures = new List<string>();

        for (var iteration = 0; iteration < 5000; iteration++)
        {
            var count = random.Next(1, 30);
            var payloads = new List<string>(count);
            var rows = new List<string>(count);

            for (var i = 0; i < count; i++)
            {
                var payload = RandomRow(random);
                payloads.Add(payload);
                rows.Add($"{i:D2}|{payload}");
            }

            var width = random.Next(20, 100);
            var height = count + random.Next(0, 6);
            var addAfterLoad = random.Next(2) == 0;

            var lines = Capture(rows, BarePanelLayout, width, height, addAfterLoad);

            for (var i = 0; i < count; i++)
            {
                // Column 0 is the TextBlock's Padding="1,0,0,0".
                var marker = lines[i].Substring(1, 3);
                if (marker != $"{i:D2}|")
                {
                    failures.Add(
                        $"iteration {iteration}: row {i} of {count} at {width}x{height} " +
                        $"(addAfterLoad: {addAfterLoad}) — line {i} starts \"{marker}\", " +
                        $"row text {Describe(payloads[i])}");
                    break;
                }
            }

            if (failures.Count > 5)
            {
                break;
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    private static string RandomRow(Random random)
    {
        // A single space is the documented spacer; an empty string legitimately measures to no
        // height, so it is excluded — that case is covered by EmptyRow_IsSkipped below.
        var length = random.Next(1, 60);
        var builder = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            builder.Append(Alphabet[random.Next(Alphabet.Length)]);
        }

        var text = builder.ToString();
        return text.Length == 0 ? " " : text;
    }

    private static string Describe(string text)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in text)
        {
            builder.Append(c < 0x20 || c > 0x7e ? $"\\u{(int)c:x4}" : c.ToString());
        }

        return builder.Append("\" (").Append(text.Length).Append(" chars)").ToString();
    }

    /// <summary>
    /// The same sweep through the shortcut dialog's own geometry — a fixed-size bordered window
    /// drawn into a larger terminal — with descriptions built from words rather than noise, since
    /// the row that went missing in the wild was ordinary English with a colon and a semicolon in it.
    /// </summary>
    [Test]
    public async Task Fuzz_ShortcutShapedRowsAllRender()
    {
        var random = new Random(2031);
        var failures = new List<string>();

        for (var iteration = 0; iteration < 5000 && failures.Count == 0; iteration++)
        {
            var count = random.Next(4, 40);
            var descriptions = new List<string>(count);
            var rows = new List<string>(count);

            for (var i = 0; i < count; i++)
            {
                var description = RandomDescription(random);
                descriptions.Add(description);
                rows.Add($"{i:D2}| {Keys[random.Next(Keys.Length)].PadRight(13)}{description}");
            }

            var windowWidth = random.Next(40, 100);
            var windowHeight = random.Next(count + 2, count + 8);
            var bufferWidth = windowWidth + random.Next(0, 60);
            var bufferHeight = random.Next(count + 1, windowHeight + 4);

            var layout = Layout.Replace(
                "Title=\"shortcuts\"",
                $"Title=\"shortcuts\" Width=\"{windowWidth}\" Height=\"{windowHeight}\"",
                StringComparison.Ordinal);

            var lines = Capture(rows, layout, bufferWidth, bufferHeight);

            for (var i = 0; i < count && 1 + i < bufferHeight; i++)
            {
                // Border 1 + TextBlock padding 1.
                var marker = lines[1 + i].Substring(2, 3);
                if (marker != $"{i:D2}|")
                {
                    failures.Add(
                        $"iteration {iteration}: row {i} of {count}, window {windowWidth}x{windowHeight} " +
                        $"in {bufferWidth}x{bufferHeight} — line {1 + i} starts \"{marker}\", " +
                        $"description {Describe(descriptions[i])}");
                    break;
                }
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    private static readonly string[] Keys =
    [
        "enter", "esc", "up/down", "left/right", "tab", "1 / 2 / 3", "q", "l", "t", "f",
        "w", "s", "r", ":", "?", "n / p", "space", "c", "backspace",
    ];

    private static readonly string[] Words =
    [
        "open", "the", "selected", "row", "back", "one", "level", "—", "clears", "a", "filter",
        "first", "move", "selection", "switch", "tab", "focus", "between", "panes", "jump", "to",
        "prod", "/", "test", "dev", "quit", "logs", "errors,", "traces,", "console,", "http,",
        "platform", "topology", "for", "current", "environment", "resource", "list", "by", "kind",
        "resource:", "its", "detail;", "an", "error:", "trace", "period", "how", "far", "everything",
        "reads", "search", "or", "waterfall", "refresh", "now", "run", "ad-hoc", "KQL", "query",
        "this", "next", "previous", "problem", "fold", "unfold", "span's", "children", "copy",
        "whole", "transaction", "as", "JSON", "re-centre", "graph", "on", "node", "centre",
        "app", "service", "read-only;", "it", "never", "writes", "Azure",
    ];

    private static string RandomDescription(Random random)
    {
        var builder = new StringBuilder();
        var words = random.Next(1, 12);

        for (var i = 0; i < words; i++)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Words[random.Next(Words.Length)]);
        }

        return builder.ToString();
    }

    // ─── Every length, and every real description in isolation ───────

    [Test]
    public async Task EveryDescriptionLength_ProducesARow()
    {
        var failures = new List<int>();

        for (var length = 1; length <= 120; length++)
        {
            var rows = new List<string>
            {
                "  before",
                "    enter        " + new string('x', length),
                "  after",
            };

            var lines = Capture(rows, BarePanelLayout, 68, 8);
            if (FirstMissingRow(rows, lines, top: 0, left: 1, room: 67) >= 0)
            {
                failures.Add(length);
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>
    /// The documented, understood case: an empty string measures to no height and the panel
    /// skips it, which is why a spacer row has to be a single space. Pinned so the workaround
    /// in consumers does not quietly stop being necessary — or quietly stop working.
    /// </summary>
    [Test]
    public async Task EmptyRow_IsSkipped_ButASingleSpaceIsNot()
    {
        var withEmpty = Capture(["one", "", "two"], BarePanelLayout, 20, 6);
        var withSpace = Capture(["one", " ", "two"], BarePanelLayout, 20, 6);

        await Assert.That(withEmpty[1].Trim()).IsEqualTo("two");
        await Assert.That(withSpace[2].Trim()).IsEqualTo("two");
    }

    // ─── The collection changing under the layout pass ───────────────

    /// <summary>
    /// A child that runs a callback the first time it is measured, or the first time it is drawn.
    /// </summary>
    private sealed class MutatingChild : FrameworkElement
    {
        public Action? OnMeasuring { get; set; }

        public Action? OnRendering { get; set; }

        public override Size2D GetPreferredSize(Rect parent)
        {
            var callback = OnMeasuring;
            OnMeasuring = null;
            callback?.Invoke();

            return new Size2D(1, 1);
        }

        public override Rect CalculateBounds(Rect parent) => parent;

        protected override void OnRender(CellBuffer buffer, Rect parentBounds)
        {
            var callback = OnRendering;
            OnRendering = null;
            callback?.Invoke();
        }
    }

    /// <summary>
    /// Builds the bare list, hands back the panel, and lets the caller plant a child that mutates
    /// the source collection at a chosen point in the layout pass.
    /// </summary>
    private static string[] CaptureWithMutation(
        int rowCount,
        Func<ObservableCollection<TextRow>, MutatingChild> plant,
        int width = 30,
        int height = 14)
    {
        var vm = new RowsViewModel();
        for (var i = 0; i < rowCount; i++)
        {
            vm.Lines.Add(new TextRow { Text = $"{i:D2}|row" });
        }

        var window = TerminalXaml.Load<Window>(BarePanelLayout, vm);
        var panel = FindItemsControl(window)!.ItemsPanel;

        // In front of the rows, so the mutation lands before the panel has drawn any of them.
        panel.Children.Insert(0, plant(vm.Lines));

        var buffer = new CellBuffer(width, height);
        window.Render(buffer, new Rect(0, 0, width, height));

        var lines = new string[height];
        for (var y = 0; y < height; y++)
        {
            var line = new StringBuilder(width);
            for (var x = 0; x < width; x++)
            {
                var codepoint = buffer[x, y].Codepoint;
                line.Append(codepoint == 0 ? " " : char.ConvertFromUtf32((int)codepoint));
            }

            // Column 0 is the template TextBlock's Padding="1,0,0,0".
            lines[y] = line.ToString().Trim();
        }

        return lines;
    }

    private static ItemsControl? FindItemsControl(Visual root)
    {
        if (root is ItemsControl itemsControl)
        {
            return itemsControl;
        }

        foreach (var (child, _) in root.GetChildrenWithBounds(new Rect(0, 0, 100, 100)))
        {
            if (FindItemsControl(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// A panel measures every child and then draws every child. If the collection shrinks in
    /// between — an item removed while an earlier sibling is being measured — the sizes and the
    /// children no longer line up by index, and the row after the removal is drawn in the slot of
    /// the row before it. Exactly one row is then never emitted and every row below it moves up by
    /// one: no clipping, no overlap, no exception, and a list that reads as complete.
    /// </summary>
    /// <remarks>
    /// This is the condition behind the vanished shortcut row. The panel now takes one snapshot of
    /// its children for the whole pass, so the frame is drawn from the list it measured; the
    /// removal shows up on the next frame, which the mutation has already invalidated.
    /// </remarks>
    [Test]
    public async Task EveryRowIsRendered_WhenAnItemIsRemovedWhileThePanelIsMeasuring()
    {
        var lines = CaptureWithMutation(8, rows => new MutatingChild { OnMeasuring = () => rows.RemoveAt(5) });

        // Row 05 is the one that used to vanish, taking 06 and 07 up a line with it.
        await Assert.That(lines[1..9]).IsEquivalentTo(
            ["00|row", "01|row", "02|row", "03|row", "04|row", "05|row", "06|row", "07|row"]);
    }

    /// <summary>
    /// The same mismatch one phase later: the collection shrinks while the panel is part-way
    /// through drawing it. Indexing the live collection then walked off the end of it.
    /// </summary>
    [Test]
    public async Task EveryRowIsRendered_WhenAnItemIsRemovedWhileThePanelIsRendering()
    {
        var lines = CaptureWithMutation(8, rows => new MutatingChild { OnRendering = () => rows.RemoveAt(6) });

        await Assert.That(lines[1..9]).IsEquivalentTo(
            ["00|row", "01|row", "02|row", "03|row", "04|row", "05|row", "06|row", "07|row"]);
    }

    /// <summary>
    /// Items that are equal to one another share a key in the container map. Each occurrence must
    /// still get its own row — a list of value-equal records is not a list with duplicates removed.
    /// </summary>
    [Test]
    public async Task ValueEqualItems_EachGetTheirOwnRow()
    {
        var items = new ObservableCollection<ValueRow>
        {
            new("first"), new("same"), new("same"), new("last"),
        };

        var control = new ItemsControl { ItemsSource = items };
        var appended = new ObservableCollection<ValueRow>();
        var afterBinding = new ItemsControl { ItemsSource = appended };

        foreach (var item in items)
        {
            appended.Add(item);
        }

        await Assert.That(control.ItemsPanel.Children.Count).IsEqualTo(4);
        await Assert.That(afterBinding.ItemsPanel.Children.Count).IsEqualTo(4);
    }

    private sealed record ValueRow(string Text);

    // ─── The real content ────────────────────────────────────────────

    private static List<string> HelpRows()
    {
        var rows = new List<string>();

        void Blank() => rows.Add(" ");

        void Section(string title)
        {
            Blank();
            rows.Add("  " + title);
        }

        void Key(string key, string description) => rows.Add($"    {key.PadRight(13)}{description}");

        Section("navigation");
        Key("enter", "open the selected row");
        Key("esc", "back one level — clears a filter first");
        Key("up/down", "move the selection");
        Key("left/right", "switch tab");
        Key("tab", "move focus between panes");
        Key("1 / 2 / 3", "jump to prod / test / dev");
        Key("q", "quit");

        Section("views");
        Key("l", "logs — errors, traces, console, http, platform");
        Key("t", "topology for the current environment");
        Key("f", "filter the resource list by kind");
        Key("enter", "a resource: its detail; an error: its trace");

        Section("reading");
        Key("w", "period — how far back everything reads");
        Key("s", "search — filter the current tab or waterfall");
        Key("r", "refresh now");
        Key(":", "run an ad-hoc KQL query");
        Key("?", "this list");

        Section("transaction");
        Key("n / p", "next / previous problem");
        Key("space", "fold or unfold a span's children");
        Key("c", "copy the whole transaction as JSON");

        Section("topology graph");
        Key("enter", "re-centre the graph on the selected node");
        Key("backspace", "back to the previous centre");
        Key("n / p", "next / previous app service");

        Blank();
        rows.Add("  the tool is read-only; it never writes to Azure");

        return rows;
    }
}
