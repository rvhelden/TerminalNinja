using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Highlighting;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace NinjaShellUi;

/// <summary>
/// Owns the mouse-driven hover panel: the underlying <see cref="HoverPanel"/>, a
/// reusable <see cref="HoverBox"/> content host, and a last-cell cache so identical
/// mouse positions don't churn the overlay.
/// </summary>
internal sealed class MouseHoverController
{
    private readonly HoverPanel _panel = new();
    private readonly HoverBox _box = new();
    private (int X, int Y)? _lastCell;

    public bool IsOpen => _panel.IsOpen;

    public SyntaxTheme Theme
    {
        get => _box.Theme;
        set => _box.Theme = value;
    }

    /// <summary>Forward a key to the hover panel. Returns true when the panel consumed it.</summary>
    public bool ForwardKey(KeyEvent e) => _panel.IsOpen && _box.HandleKey(e);

    /// <summary>True when the mouse landed on a new cell — i.e. caller should recompute.</summary>
    public bool TryMoveCursor(int x, int y)
    {
        var cell = (x, y);
        if (_lastCell == cell) return false;
        _lastCell = cell;
        return true;
    }

    public void ShowInputHover(int mouseX, int mouseY, string text, Position position, IReadOnlyDictionary<string, NValue>? scope)
    {
        var hover = LanguageService.GetHover(text, position, scope);
        if (hover is null)
        {
            Hide();
            return;
        }

        _box.Language = "ninja";
        var content = BuildContent(hover.Contents);
        _panel.Placement = PlacementMode.Top;
        _panel.ShowAt(mouseX, mouseY, content);
    }

    public void ShowValueHover(NValue value, int mouseX, int mouseY)
    {
        var sb = new StringBuilder();
        sb.Append("result :: ").AppendLine(ValueFormatter.TypeName(value));
        sb.AppendLine();
        sb.Append("shape: ").AppendLine(ValueFormatter.Def(value));
        sb.Append("data:  ").Append(ValueFormatter.Dump(value));

        // Value hovers surface obj.dump-style payloads — drive the highlighter
        // with the record grammar so keys/values are visually distinguishable.
        _box.Language = "record";
        var content = BuildContent(sb.ToString());
        _panel.Placement = PlacementMode.Bottom;
        _panel.ShowAt(mouseX, mouseY, content);
    }

    public void Hide()
    {
        if (_panel.IsOpen) _panel.Hide();
        _lastCell = null;
    }

    private UIElement BuildContent(string text)
    {
        _box.Text = text;
        return _box;
    }
}
