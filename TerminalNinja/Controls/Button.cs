using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// An interactive button control that responds to focus, hover, and click events.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class Button : ButtonBase
{
    public Button()
    {
        DefaultStyleKey = typeof(Button);
        Padding = new Thickness(2, 0);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(Button),
            new FrameworkPropertyMetadata("", affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(Button),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(Button),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(Button),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(Button),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    /// <summary>Gets or sets the button label text.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty)!;
        set => SetValue(TextProperty, value);
    }
    
    /// <summary>Gets or sets the focus border color.</summary>
    public Color FocusColor
    {
        get => (Color)GetValue(FocusColorProperty)!;
        set => SetValue(FocusColorProperty, value);
    }
    
    /// <summary>Gets or sets the hover border color.</summary>
    public Color HoverColor
    {
        get => (Color)GetValue(HoverColorProperty)!;
        set => SetValue(HoverColorProperty, value);
    }
    
    /// <summary>Gets or sets the width (absolute, relative, or stretch).</summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }
    
    /// <summary>Gets or sets the height (absolute, relative, or stretch).</summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }
    
    /// <summary>
    /// Returns the preferred size of this button based on text length.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        var textWidth = Text.Length + Padding.HorizontalTotal;
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : textWidth;
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : 2 + Padding.VerticalTotal + 1;
        return new Size2D(w, h);
    }
    
    /// <summary>
    /// Calculates the absolute bounds of this button within the parent bounds.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        var preferred = GetPreferredSize(parent);
        var w = Width.Mode == SizeMode.Auto ? preferred.Width : Width.Resolve(parent.Width);
        var h = Height.Mode == SizeMode.Auto ? preferred.Height : Height.Resolve(parent.Height);
        
        return ApplyAlignment(parent, w, h);
    }
    
    /// <summary>
    /// Renders this button to the specified cell buffer.
    /// </summary>
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        // Choose border color based on focus/hover state (dimmed if disabled)
        var borderColor = IsFocused ? FocusColor : IsMouseOver ? HoverColor : Foreground;
        if (!IsEnabled)
        {
            borderColor = new Color((byte)(borderColor.R / 2), (byte)(borderColor.G / 2), (byte)(borderColor.B / 2)); // Dim by 50%
        }

        // Create rounded border with appropriate color
        var border = BorderStyle.Rounded(borderColor);
        
        // Fill background
        var bgCell = new Cell(' ', Foreground, Background);
        buffer.FillRect(clipped, bgCell);
        
        // Draw border
        DrawBorder(buffer, bounds, border.Chars, borderColor);
        
        // Draw text centered in the padded content area
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var textColor = IsEnabled ? Foreground : new Color((byte)(Foreground.R / 2), (byte)(Foreground.G / 2), (byte)(Foreground.B / 2));
        var contentX = bounds.X + 1 + Padding.Left; // 1 for border
        var contentWidth = Math.Max(0, bounds.Width - 2 - Padding.HorizontalTotal);
        var textX = contentX + (contentWidth - Text.Length) / 2;
        var textY = bounds.Y + 1 + Padding.Top;
            
        for (var i = 0; i < Text.Length && textX + i < bounds.X + bounds.Width; i++)
        {
            var charX = textX + i;
                
            // Skip characters outside buffer
            if (charX < 0 || charX >= buffer.Width || textY < 0 || textY >= buffer.Height)
            {
                continue;
            }

            buffer.SetChar(charX, textY, Text[i], textColor, Background);
        }
    }
    
    /// <summary>
    /// Draws the border around the button.
    /// </summary>
    private void DrawBorder(CellBuffer buffer, Rect bounds, BorderChars chars, Color color)
    {
        if (chars.IsEmpty)
        {
            return;
        }

        // Top and bottom edges
        for (var x = bounds.X + 1; x < bounds.X + bounds.Width - 1; x++)
        {
            if (x < 0 || x >= buffer.Width)
            {
                continue;
            }

            if (bounds.Y >= 0 && bounds.Y < buffer.Height)
            {
                buffer.SetChar(x, bounds.Y, chars.Horizontal, color, Background);
            }

            if (bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
            {
                buffer.SetChar(x, bounds.Y + bounds.Height - 1, chars.Horizontal, color, Background);
            }
        }
        
        // Left and right edges
        for (var y = bounds.Y + 1; y < bounds.Y + bounds.Height - 1; y++)
        {
            if (y < 0 || y >= buffer.Height)
            {
                continue;
            }

            if (bounds.X >= 0 && bounds.X < buffer.Width)
            {
                buffer.SetChar(bounds.X, y, chars.Vertical, color, Background);
            }

            if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width)
            {
                buffer.SetChar(bounds.X + bounds.Width - 1, y, chars.Vertical, color, Background);
            }
        }
        
        // Corners
        if (bounds.X >= 0 && bounds.X < buffer.Width && bounds.Y >= 0 && bounds.Y < buffer.Height)
        {
            buffer.SetChar(bounds.X, bounds.Y, chars.TopLeft, color, Background);
        }

        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width &&
            bounds.Y >= 0 && bounds.Y < buffer.Height)
        {
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y, chars.TopRight, color, Background);
        }

        if (bounds.X >= 0 && bounds.X < buffer.Width &&
            bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
        {
            buffer.SetChar(bounds.X, bounds.Y + bounds.Height - 1, chars.BottomLeft, color, Background);
        }

        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width &&
            bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
        {
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, chars.BottomRight, color, Background);
        }
    }
    
    /// <summary>
    /// Handles keyboard events when the button has focus.
    /// </summary>
    public override bool OnKeyEvent(KeyEvent e)
    {
        // Trigger click on Enter or Space
        if (e.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
        {
            RaiseClick();
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Handles mouse events when the mouse is over the button.
    /// </summary>
    public override void OnMouseEvent(MouseEvent e)
    {
        // Trigger click on left mouse button press
        if (e is { Action: MouseAction.Press, Button: MouseButton.Left })
        {
            RaiseClick();
        }
    }
}
