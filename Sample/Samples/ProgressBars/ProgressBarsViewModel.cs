using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.ProgressBars;

public class ProgressBarsViewModel : ViewModelBase, IDisposable
{
    public double ProgressValue
    {
        get;
        set => SetProperty(ref field, value);
    }

    private readonly Timer _timer;

    public ProgressBarsViewModel()
    {
        _timer = new Timer(_ =>
        {
            ProgressValue = (ProgressValue + 2) % 102;
            if (ProgressValue > 100) ProgressValue = 0;
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}
