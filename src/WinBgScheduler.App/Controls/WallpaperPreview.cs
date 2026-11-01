using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WinBgScheduler.App.Services;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Scheduling;

namespace WinBgScheduler.App.Controls;

/// <summary>A monitor-shaped preview that places the image the way Windows would for the chosen fit.</summary>
public sealed class WallpaperPreview : Border
{
    private const int PreviewDecodeWidth = 1600;

    public static readonly DependencyProperty ImagePathProperty = Register(nameof(ImagePath), typeof(string), null);
    public static readonly DependencyProperty FitProperty = Register(nameof(Fit), typeof(FitMode), FitMode.Fill);
    public static readonly DependencyProperty MonitorWidthProperty = Register(nameof(MonitorWidth), typeof(double), 1920d);
    public static readonly DependencyProperty MonitorHeightProperty = Register(nameof(MonitorHeight), typeof(double), 1080d);

    public WallpaperPreview()
    {
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(6);
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24));
        Background = Brushes.Black;
        Rebuild();
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public FitMode Fit
    {
        get => (FitMode)GetValue(FitProperty);
        set => SetValue(FitProperty, value);
    }

    public double MonitorWidth
    {
        get => (double)GetValue(MonitorWidthProperty);
        set => SetValue(MonitorWidthProperty, value);
    }

    public double MonitorHeight
    {
        get => (double)GetValue(MonitorHeightProperty);
        set => SetValue(MonitorHeightProperty, value);
    }

    private static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(WallpaperPreview),
            new PropertyMetadata(defaultValue, (d, _) => ((WallpaperPreview)d).Rebuild()));

    private void Rebuild()
    {
        var w = Math.Max(1, MonitorWidth);
        var h = Math.Max(1, MonitorHeight);
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true, Background = Brushes.Black };

        if (ImagePath is { } path && ImageLoader.PixelSize(path) is { } size && ImageLoader.Load(path, PreviewDecodeWidth) is { } image)
        {
            var placed = FitGeometry.Place(Fit == FitMode.Span ? FitMode.Fill : Fit, size.Width, size.Height, w, h);
            UIElement element = Fit == FitMode.Tile
                ? new Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = new ImageBrush(image)
                    {
                        TileMode = TileMode.Tile,
                        ViewportUnits = BrushMappingMode.Absolute,
                        Viewport = new Rect(0, 0, placed.Width, placed.Height),
                    },
                }
                : new Image { Source = image, Stretch = Stretch.Fill, Width = placed.Width, Height = placed.Height };
            if (Fit != FitMode.Tile)
            {
                Canvas.SetLeft(element, placed.X);
                Canvas.SetTop(element, placed.Y);
            }
            RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.HighQuality);
            canvas.Children.Add(element);
        }
        else
        {
            var hint = new TextBlock
            {
                Text = "Sem imagem",
                Foreground = Brushes.Gray,
                FontSize = h / 18,
                Width = w,
                TextAlignment = TextAlignment.Center,
            };
            Canvas.SetTop(hint, h / 2 - h / 30);
            canvas.Children.Add(hint);
        }

        Child = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };
    }
}
