using TerminalNinja.Primitives;
using TerminalNinja.Xaml.Mvvm;

namespace Sample.Samples.Image;

public class ImageViewModel : ViewModelBase
{
    /// <summary>
    /// A simple gradient test image (32x16 pixels).
    /// </summary>
    public Color[,] GradientImage { get; }

    public ImageViewModel()
    {
        const int w = 32;
        const int h = 16;
        GradientImage = new Color[w, h];

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var r = (byte)(x * 255 / (w - 1));
                var g = (byte)(y * 255 / (h - 1));
                var b = (byte)(128);
                GradientImage[x, y] = new Color(r, g, b);
            }
        }
    }
}
