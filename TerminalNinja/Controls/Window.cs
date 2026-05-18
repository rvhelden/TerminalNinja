using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A top-level container control representing a window in the terminal UI.
/// Window is a ContentControl that holds Content and provides window-scoped resources.
/// Supports both non-modal (Show) and modal (ShowDialogAsync) display modes.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class Window : ContentControl
{
    private TaskCompletionSource<bool?>? _dialogTcs;

    public Window()
    {
        DefaultStyleKey = typeof(Window);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Window),
            new FrameworkPropertyMetadata("", affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(Window),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(Window),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty DialogResultProperty =
        DependencyProperty.Register(nameof(DialogResult), typeof(bool?), typeof(Window),
            new PropertyMetadata((object?)null, OnDialogResultChanged));

    /// <summary>
    /// Gets or sets the window title.
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the width of the window.
    /// Use Size.Stretch (default) to fill available width.
    /// </summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the height of the window.
    /// Use Size.Stretch (default) to fill available height.
    /// </summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the dialog result. Setting this property while the window
    /// is shown as a modal dialog will close the dialog and complete the
    /// <see cref="ShowDialogAsync"/> task with the specified value.
    /// </summary>
    public bool? DialogResult
    {
        get => (bool?)GetValue(DialogResultProperty);
        set => SetValue(DialogResultProperty, value);
    }

    /// <summary>
    /// Gets whether this window is currently displayed as a modal dialog.
    /// </summary>
    public bool IsModal { get; private set; }

    // ─── Modal Dialog Support ────────────────────────────────────────

    /// <summary>
    /// Shows this window as a modal dialog on top of the current root control.
    /// Input is restricted to this window until it is closed.
    /// The background is dimmed to indicate modality.
    /// </summary>
    /// <returns>
    /// A task that completes with the <see cref="DialogResult"/> when the dialog
    /// is closed. Returns null if the dialog was closed without setting a result.
    /// </returns>
    public Task<bool?> ShowDialogAsync()
    {
        var app = App.Application.Current
            ?? throw new InvalidOperationException("Cannot show dialog: no Application instance.");

        if (IsModal)
        {
            throw new InvalidOperationException("This window is already shown as a modal dialog.");
        }

        IsModal = true;
        DialogResult = null;
        _dialogTcs = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Push as a modal overlay with background dimming
        app.PushOverlay(this, isModal: true, dimBackground: true);

        // Move focus into the dialog so keyboard input works immediately.
        // Focus the dialog window itself — it is Focusable (inherits from Control)
        // so key events dispatched via FocusManager will reach OnKeyEvent.
        app.FocusManager.SetFocus(this);

        return _dialogTcs.Task;
    }

    /// <summary>
    /// Closes this modal dialog, completing the <see cref="ShowDialogAsync"/> task.
    /// If <see cref="DialogResult"/> has not been set, it defaults to null.
    /// </summary>
    public void CloseDialog()
    {
        if (!IsModal)
        {
            return;
        }

        var app = App.Application.Current;
        app?.RemoveOverlay(this);

        IsModal = false;
        var result = DialogResult;
        _dialogTcs?.TrySetResult(result);
        _dialogTcs = null;

        // Restore focus to the main visual tree
        app?.FocusManager.SetFocus(null);
    }

    private static void OnDialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window { IsModal: true } window && e.NewValue != null)
        {
            // Setting DialogResult on an active modal closes the dialog
            window.CloseDialog();
        }
    }

    // ─── Layout & Rendering ──────────────────────────────────────────

    /// <summary>
    /// Returns the preferred size of the window content.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (!HasContent)
        {
            return new Size2D(0, 0);
        }

        return base.GetPreferredSize(parent);
    }
    
    /// <summary>
    /// Calculates the bounds of this window within the parent bounds.
    /// When shown as a modal dialog, the window is centered within the parent.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        
        var x = parent.X;
        var y = parent.Y;
        
        // Center modal dialogs within the viewport
        if (IsModal)
        {
            if (w < parent.Width)
            {
                x = parent.X + (parent.Width - w) / 2;
            }

            if (h < parent.Height)
            {
                y = parent.Y + (parent.Height - h) / 2;
            }
        }
        
        return new Rect(x, y, w, h);
    }
    
    /// <summary>
    /// Renders the window content to the buffer.
    /// Window itself has no visual representation - it delegates to Content via ContentPresenter.
    /// </summary>
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Render content if present (delegated to ContentPresenter via base)
        if (HasContent)
        {
            base.OnRender(buffer, bounds);
        }
    }

    /// <summary>
    /// Shows this window by setting it as the root control of the current Application.
    /// </summary>
    public void Show()
    {
        var app = App.Application.Current;
        if (app != null)
        {
            app.RootControl = this;
        }
    }
    
    /// <summary>
    /// Closes this window. If shown as a modal dialog, closes the dialog.
    /// If shown as the root control, clears the root.
    /// </summary>
    public void Close()
    {
        if (IsModal)
        {
            CloseDialog();
            return;
        }
        
        var app = App.Application.Current;
        if (app?.RootControl == this)
        {
            app.RootControl = null;
        }
    }
}
