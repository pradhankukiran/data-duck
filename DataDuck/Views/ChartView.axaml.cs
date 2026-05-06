using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using DataDuck.Models;
using DataDuck.ViewModels;

namespace DataDuck.Views;

public partial class ChartView : UserControl
{
    private Canvas? _lineCanvas;
    private Canvas? _pieCanvas;
    private ChartViewModel? _hookedVm;

    public ChartView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        DataContextChanged += (_, _) => RehookViewModel();
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _lineCanvas = this.FindControl<Canvas>("LineCanvas");
        _pieCanvas = this.FindControl<Canvas>("PieCanvas");

        if (_lineCanvas is not null)
            _lineCanvas.SizeChanged += OnLineCanvasSizeChanged;
        if (_pieCanvas is not null)
            _pieCanvas.SizeChanged += OnPieCanvasSizeChanged;

        RehookViewModel();
        RedrawAll();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_lineCanvas is not null)
            _lineCanvas.SizeChanged -= OnLineCanvasSizeChanged;
        if (_pieCanvas is not null)
            _pieCanvas.SizeChanged -= OnPieCanvasSizeChanged;
        UnhookViewModel();
    }

    private void OnLineCanvasSizeChanged(object? sender, SizeChangedEventArgs e) => RedrawLine();
    private void OnPieCanvasSizeChanged(object? sender, SizeChangedEventArgs e) => RedrawPie();

    private void RehookViewModel()
    {
        UnhookViewModel();
        if (DataContext is ChartViewModel vm)
        {
            _hookedVm = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
            vm.LinePoints.CollectionChanged += (_, _) => RedrawLine();
            vm.PieSlices.CollectionChanged += (_, _) => RedrawPie();
        }
        RedrawAll();
    }

    private void UnhookViewModel()
    {
        if (_hookedVm is not null)
        {
            _hookedVm.PropertyChanged -= OnVmPropertyChanged;
            _hookedVm = null;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChartViewModel.Kind))
            RedrawAll();
    }

    private void RedrawAll()
    {
        RedrawLine();
        RedrawPie();
    }

    // ----- Line chart drawing -----

    private void RedrawLine()
    {
        if (_lineCanvas is null) return;
        _lineCanvas.Children.Clear();

        if (DataContext is not ChartViewModel vm) return;
        if (vm.Kind != ChartKind.Line) return;
        if (vm.LinePoints.Count < 2) return;

        var w = _lineCanvas.Bounds.Width;
        var h = _lineCanvas.Bounds.Height;
        if (w <= 4 || h <= 4) return;

        const double padX = 6.0;
        const double padTop = 8.0;
        const double padBottom = 8.0;

        double xMin = double.MaxValue, xMax = double.MinValue;
        double yMin = double.MaxValue, yMax = double.MinValue;
        foreach (var p in vm.LinePoints)
        {
            if (p.X < xMin) xMin = p.X;
            if (p.X > xMax) xMax = p.X;
            if (p.Y < yMin) yMin = p.Y;
            if (p.Y > yMax) yMax = p.Y;
        }
        var xRange = xMax - xMin;
        if (xRange <= 0) return;
        var yRange = yMax - yMin;
        if (yRange <= 0) yRange = 1; // flat line: avoid divide-by-zero

        var plotW = Math.Max(1.0, w - 2 * padX);
        var plotH = Math.Max(1.0, h - padTop - padBottom);

        var accent = TryGetBrush("DdAccentBrush") ?? new SolidColorBrush(Color.Parse("#F4C430"));

        var polyline = new Polyline
        {
            Stroke = accent,
            StrokeThickness = 2,
            StrokeJoin = PenLineJoin.Round,
            StrokeLineCap = PenLineCap.Round,
        };
        var pts = new Points();

        foreach (var p in vm.LinePoints)
        {
            var x = padX + (p.X - xMin) / xRange * plotW;
            var y = padTop + (yMax - p.Y) / yRange * plotH;
            pts.Add(new Point(x, y));
        }
        polyline.Points = pts;
        _lineCanvas.Children.Add(polyline);

        // Dots at each data point.
        foreach (var pt in pts)
        {
            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = accent,
            };
            Canvas.SetLeft(dot, pt.X - 2.5);
            Canvas.SetTop(dot, pt.Y - 2.5);
            _lineCanvas.Children.Add(dot);
        }
    }

    // ----- Pie chart drawing -----

    private void RedrawPie()
    {
        if (_pieCanvas is null) return;
        _pieCanvas.Children.Clear();

        if (DataContext is not ChartViewModel vm) return;
        if (vm.Kind != ChartKind.Pie) return;
        if (vm.PieSlices.Count == 0) return;

        var w = _pieCanvas.Bounds.Width;
        var h = _pieCanvas.Bounds.Height;
        if (w <= 4 || h <= 4) return;

        var diameter = Math.Min(w, h) - 16;
        if (diameter <= 8) return;
        var radius = diameter / 2.0;
        var cx = w / 2.0;
        var cy = h / 2.0;

        // Special-case: a single slice represents 100%; ArcSegment cannot draw a full
        // 360° sweep with the same start/end point, so draw an Ellipse instead.
        if (vm.PieSlices.Count == 1)
        {
            var only = vm.PieSlices[0];
            var fill = ParseHexBrush(only.ColorHex);
            var ellipse = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = fill,
            };
            Canvas.SetLeft(ellipse, cx - radius);
            Canvas.SetTop(ellipse, cy - radius);
            _pieCanvas.Children.Add(ellipse);
            return;
        }

        foreach (var slice in vm.PieSlices)
        {
            if (slice.SweepAngleDeg <= 0) continue;
            var startRad = slice.StartAngleDeg * Math.PI / 180.0;
            var endRad = (slice.StartAngleDeg + slice.SweepAngleDeg) * Math.PI / 180.0;
            var startPt = new Point(cx + radius * Math.Cos(startRad),
                                    cy + radius * Math.Sin(startRad));
            var endPt = new Point(cx + radius * Math.Cos(endRad),
                                  cy + radius * Math.Sin(endRad));
            var isLargeArc = slice.SweepAngleDeg > 180.0;

            var figure = new PathFigure
            {
                StartPoint = new Point(cx, cy),
                IsClosed = true,
                IsFilled = true,
            };
            figure.Segments!.Add(new LineSegment { Point = startPt });
            figure.Segments!.Add(new ArcSegment
            {
                Point = endPt,
                Size = new Size(radius, radius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Clockwise,
                RotationAngle = 0,
            });

            var geom = new PathGeometry();
            geom.Figures!.Add(figure);

            var path = new Path
            {
                Data = geom,
                Fill = ParseHexBrush(slice.ColorHex),
                Stroke = Brushes.White,
                StrokeThickness = 1,
            };
            _pieCanvas.Children.Add(path);
        }
    }

    private static IBrush ParseHexBrush(string hex)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch
        {
            return new SolidColorBrush(Color.Parse("#F4C430"));
        }
    }

    private IBrush? TryGetBrush(string key)
    {
        if (Application.Current is { } app &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var v) &&
            v is IBrush b)
        {
            return b;
        }
        return null;
    }
}
