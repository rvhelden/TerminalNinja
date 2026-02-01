using System.ComponentModel;
using System.Runtime.CompilerServices;
using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Elements;

/// <summary>
/// Base class for all elements providing INotifyPropertyChanged and invalidation support.
/// </summary>
public abstract class ElementBase : IElement, INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
    
    // IElement properties
    private string? _name;
    public string? Name
    {
        get => _name;
        set => SetProperty(ref _name, value, invalidate: false);
    }
    
    private object? _dataContext;
    public object? DataContext
    {
        get => _dataContext;
        set => SetProperty(ref _dataContext, value, invalidate: false);
    }
    
    public IElement? Parent { get; set; }
    
    public Action? InvalidationCallback { get; set; }
    
    /// <summary>
    /// Signals that this element needs to be re-rendered.
    /// </summary>
    public void InvalidateVisual()
    {
        InvalidationCallback?.Invoke();
    }
    
    /// <summary>
    /// Gets the effective DataContext by walking up the parent chain if needed.
    /// </summary>
    public object? GetEffectiveDataContext()
    {
        if (_dataContext != null)
            return _dataContext;
        
        return Parent switch
        {
            ElementBase eb => eb.GetEffectiveDataContext(),
            IElement e => e.DataContext,
            _ => null
        };
    }
    
    /// <summary>
    /// Sets a property value and raises PropertyChanged if the value changed.
    /// Also invalidates the visual if the property affects rendering.
    /// </summary>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="invalidate">Whether to trigger visual invalidation (default: true).</param>
    /// <param name="propertyName">The property name (automatically captured).</param>
    /// <returns>True if the value changed; otherwise, false.</returns>
    protected bool SetProperty<T>(
        ref T field, 
        T value, 
        bool invalidate = true,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        
        field = value;
        OnPropertyChanged(propertyName);
        
        if (invalidate)
            InvalidateVisual();
        
        return true;
    }
    
    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    // Abstract members that derived classes must implement
    public abstract Size2D GetPreferredSize(Rect parent);
    public abstract Rect CalculateBounds(Rect parent);
    public abstract void Render(CellBuffer buffer, Rect parentBounds);
}
