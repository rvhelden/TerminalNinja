using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Commands;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// An interactive button control that responds to focus, hover, and click events.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class Button : FrameworkElement, IFocusable
{
    // Bindable properties (with change notification)
    private string _text = "";
    /// <summary>Gets or sets the button label text.</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }
    
    private Color _backgroundColor = Color.Black;
    /// <summary>Gets or sets the background color.</summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => SetProperty(ref _backgroundColor, value);
    }
    
    private Color _foregroundColor = Color.White;
    /// <summary>Gets or sets the normal foreground color.</summary>
    public Color ForegroundColor
    {
        get => _foregroundColor;
        set => SetProperty(ref _foregroundColor, value);
    }
    
    private Color _focusColor = Color.Cyan;
    /// <summary>Gets or sets the focus border color.</summary>
    public Color FocusColor
    {
        get => _focusColor;
        set => SetProperty(ref _focusColor, value);
    }
    
    private Color _hoverColor = Color.Yellow;
    /// <summary>Gets or sets the hover border color.</summary>
    public Color HoverColor
    {
        get => _hoverColor;
        set => SetProperty(ref _hoverColor, value);
    }
    
    private ICommand? _command;
    /// <summary>Gets or sets the command to execute when the button is clicked.</summary>
    public ICommand? Command
    {
        get => _command;
        set
        {
            if (_command != null)
                _command.CanExecuteChanged -= OnCanExecuteChanged;
            SetProperty(ref _command, value, invalidate: false);
            if (_command != null)
                _command.CanExecuteChanged += OnCanExecuteChanged;
            UpdateCanExecute();
        }
    }
    
    private object? _commandParameter;
    /// <summary>Gets or sets the parameter to pass to the Command.</summary>
    public object? CommandParameter
    {
        get => _commandParameter;
        set
        {
            SetProperty(ref _commandParameter, value, invalidate: false);
            UpdateCanExecute();
        }
    }
    
    private bool _isEnabled = true;
    /// <summary>Gets whether this button is enabled (based on Command.CanExecute).</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        private set => SetProperty(ref _isEnabled, value);
    }
    
    // Layout properties (kept as init for now)
    /// <summary>Gets or sets the X position (absolute, relative, or stretch).</summary>
    public Size X { get; set; } = Size.Absolute(0);
    
    /// <summary>Gets or sets the Y position (absolute, relative, or stretch).</summary>
    public Size Y { get; set; } = Size.Absolute(0);
    
    /// <summary>Gets or sets the horizontal alignment within the parent.</summary>
    public Alignment HorizontalAlignment { get; set; } = Alignment.Start;
    
    /// <summary>Gets or sets the vertical alignment within the parent.</summary>
    public Alignment VerticalAlignment { get; set; } = Alignment.Start;
    
    /// <summary>Gets or sets the width (absolute, relative, or stretch).</summary>
    public Size Width { get; set; } = Size.Absolute(10);
    
    /// <summary>Gets or sets the height (absolute, relative, or stretch).</summary>
    public Size Height { get; set; } = Size.Absolute(3);
    
    // IFocusable properties
    /// <summary>Gets or sets the tab order index.</summary>
    public int TabIndex { get; set; }
    
    /// <summary>Gets or sets whether this button can receive focus.</summary>
    public bool CanFocus { get; set; } = true;
    
    /// <summary>Gets or sets whether this button currently has focus (managed by FocusManager).</summary>
    public bool IsFocused { get; set; }
    
    /// <summary>Gets or sets whether this button is currently hovered (managed by FocusManager).</summary>
    public bool IsHovered { get; set; }
    
    /// <summary>Event raised when the button is clicked (for backwards compatibility).</summary>
    public event Action? Click;
    
    private void OnCanExecuteChanged(object? sender, EventArgs e) => UpdateCanExecute();
    
    private void UpdateCanExecute()
    {
        IsEnabled = _command?.CanExecute(_commandParameter) ?? true;
    }
    
    /// <summary>
    /// Returns the preferred size of this button based on text length.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        // Button size is text length + 4 (2 chars padding on each side)
        var textWidth = Text.Length + 4;
        // Respect explicit Width/Height when set to Absolute mode
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : textWidth;
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : 3;
        return new Size2D(w, h);
    }
    
    /// <summary>
    /// Calculates the absolute bounds of this button within the parent bounds.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        
        var xOffset = X.Resolve(parent.Width);
        var yOffset = Y.Resolve(parent.Height);
        
        var x = HorizontalAlignment switch
        {
            Alignment.Start => parent.X + xOffset,
            Alignment.Center => parent.X + (parent.Width - w) / 2 + xOffset,
            Alignment.End => parent.X + parent.Width - w - xOffset,
            _ => parent.X + xOffset
        };
        
        var y = VerticalAlignment switch
        {
            Alignment.Start => parent.Y + yOffset,
            Alignment.Center => parent.Y + (parent.Height - h) / 2 + yOffset,
            Alignment.End => parent.Y + parent.Height - h - yOffset,
            _ => parent.Y + yOffset
        };
        
        return new Rect(x, y, w, h);
    }
    
    /// <summary>
    /// Renders this button to the specified cell buffer.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        
        // Choose border color based on focus/hover state (dimmed if disabled)
        var borderColor = IsFocused ? FocusColor : IsHovered ? HoverColor : ForegroundColor;
        if (!IsEnabled)
            borderColor = new Color((byte)(borderColor.R / 2), (byte)(borderColor.G / 2), (byte)(borderColor.B / 2)); // Dim by 50%
        
        // Create rounded border with appropriate color
        var border = Border.Rounded(borderColor);
        
        // Fill background
        var bgCell = new Cell(' ', ForegroundColor, BackgroundColor);
        buffer.FillRect(clipped, bgCell);
        
        // Draw border
        DrawBorder(buffer, bounds, border.Chars, borderColor);
        
        // Draw text centered in the button
        if (!string.IsNullOrEmpty(Text))
        {
            var textColor = IsEnabled ? ForegroundColor : new Color((byte)(ForegroundColor.R / 2), (byte)(ForegroundColor.G / 2), (byte)(ForegroundColor.B / 2));
            var textX = bounds.X + (bounds.Width - Text.Length) / 2;
            var textY = bounds.Y + bounds.Height / 2;
            
            for (var i = 0; i < Text.Length && textX + i < bounds.X + bounds.Width; i++)
            {
                var charX = textX + i;
                
                // Skip characters outside buffer
                if (charX < 0 || charX >= buffer.Width || textY < 0 || textY >= buffer.Height)
                    continue;
                
                buffer.SetChar(charX, textY, Text[i], textColor, BackgroundColor);
            }
        }
    }
    
    /// <summary>
    /// Draws the border around the button.
    /// </summary>
    private void DrawBorder(CellBuffer buffer, Rect bounds, BorderChars chars, Color color)
    {
        if (chars.IsEmpty) return;
        
        // Top and bottom edges
        for (var x = bounds.X + 1; x < bounds.X + bounds.Width - 1; x++)
        {
            if (x >= 0 && x < buffer.Width)
            {
                if (bounds.Y >= 0 && bounds.Y < buffer.Height)
                    buffer.SetChar(x, bounds.Y, chars.Horizontal, color, BackgroundColor);
                
                if (bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
                    buffer.SetChar(x, bounds.Y + bounds.Height - 1, chars.Horizontal, color, BackgroundColor);
            }
        }
        
        // Left and right edges
        for (var y = bounds.Y + 1; y < bounds.Y + bounds.Height - 1; y++)
        {
            if (y >= 0 && y < buffer.Height)
            {
                if (bounds.X >= 0 && bounds.X < buffer.Width)
                    buffer.SetChar(bounds.X, y, chars.Vertical, color, BackgroundColor);
                
                if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width)
                    buffer.SetChar(bounds.X + bounds.Width - 1, y, chars.Vertical, color, BackgroundColor);
            }
        }
        
        // Corners
        if (bounds.X >= 0 && bounds.X < buffer.Width && bounds.Y >= 0 && bounds.Y < buffer.Height)
            buffer.SetChar(bounds.X, bounds.Y, chars.TopLeft, color, BackgroundColor);
        
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width &&
            bounds.Y >= 0 && bounds.Y < buffer.Height)
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y, chars.TopRight, color, BackgroundColor);
        
        if (bounds.X >= 0 && bounds.X < buffer.Width &&
            bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
            buffer.SetChar(bounds.X, bounds.Y + bounds.Height - 1, chars.BottomLeft, color, BackgroundColor);
        
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width &&
            bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, chars.BottomRight, color, BackgroundColor);
    }
    
    /// <summary>
    /// Hit tests the button at the specified coordinates.
    /// </summary>
    public Rect? HitTest(int x, int y, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        return bounds.Contains(x, y) ? bounds : null;
    }
    
    /// <summary>
    /// Called when the button receives focus.
    /// </summary>
    public void OnFocus()
    {
        // Focus handled by FocusManager
    }
    
    /// <summary>
    /// Called when the button loses focus.
    /// </summary>
    public void OnBlur()
    {
        // Blur handled by FocusManager
    }
    
    /// <summary>
    /// Called when the mouse enters the button.
    /// </summary>
    public void OnMouseEnter()
    {
        // Hover handled by FocusManager
    }
    
    /// <summary>
    /// Called when the mouse leaves the button.
    /// </summary>
    public void OnMouseLeave()
    {
        // Hover handled by FocusManager
    }
    
    /// <summary>
    /// Handles keyboard events when the button has focus.
    /// </summary>
    public void OnKeyEvent(KeyEvent e)
    {
        // Trigger click on Enter or Space
        if (e.Key == ConsoleKey.Enter || e.Key == ConsoleKey.Spacebar)
        {
            RaiseClick();
        }
    }
    
    /// <summary>
    /// Handles mouse events when the mouse is over the button.
    /// </summary>
    public void OnMouseEvent(MouseEvent e)
    {
        // Trigger click on left mouse button press
        if (e.Action == MouseAction.Press && e.Button == MouseButton.Left)
        {
            RaiseClick();
        }
    }
    
    private void RaiseClick()
    {
        if (!IsEnabled) return;
        
        // Execute command if available
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
        
        // Also raise Click event for backwards compatibility
        Click?.Invoke();
    }
}
