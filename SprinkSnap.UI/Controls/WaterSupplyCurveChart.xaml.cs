using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI.Controls;

public partial class WaterSupplyCurveChart : UserControl
{
    public static readonly DependencyProperty SupplyCurveProperty =
        DependencyProperty.Register(
            nameof(SupplyCurve),
            typeof(IEnumerable),
            typeof(WaterSupplyCurveChart),
            new PropertyMetadata(null, OnChartDataChanged));

    public static readonly DependencyProperty DemandFlowGpmProperty =
        DependencyProperty.Register(
            nameof(DemandFlowGpm),
            typeof(double),
            typeof(WaterSupplyCurveChart),
            new PropertyMetadata(0.0, OnChartDataChanged));

    public static readonly DependencyProperty DemandPressurePsiProperty =
        DependencyProperty.Register(
            nameof(DemandPressurePsi),
            typeof(double),
            typeof(WaterSupplyCurveChart),
            new PropertyMetadata(0.0, OnChartDataChanged));

    public WaterSupplyCurveChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public IEnumerable SupplyCurve
    {
        get => (IEnumerable)GetValue(SupplyCurveProperty);
        set => SetValue(SupplyCurveProperty, value);
    }

    public double DemandFlowGpm
    {
        get => (double)GetValue(DemandFlowGpmProperty);
        set => SetValue(DemandFlowGpmProperty, value);
    }

    public double DemandPressurePsi
    {
        get => (double)GetValue(DemandPressurePsiProperty);
        set => SetValue(DemandPressurePsiProperty, value);
    }

    private static void OnChartDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaterSupplyCurveChart chart)
        {
            chart.Redraw();
        }
    }

    private void Redraw()
    {
        if (ChartCanvas == null)
        {
            return;
        }

        ChartCanvas.Children.Clear();
        List<WaterSupplyCurvePoint> points = (SupplyCurve as IEnumerable<WaterSupplyCurvePoint>)?.ToList()
            ?? SupplyCurve?.Cast<WaterSupplyCurvePoint>().ToList()
            ?? new List<WaterSupplyCurvePoint>();

        if (points.Count < 2 || ActualWidth < 40 || ActualHeight < 40)
        {
            return;
        }

        const double margin = 36.0;
        double width = ActualWidth - margin * 2;
        double height = ActualHeight - margin * 2;
        double maxFlow = points.Max(point => point.FlowGpm);
        if (DemandFlowGpm > maxFlow)
        {
            maxFlow = DemandFlowGpm;
        }

        double maxPressure = points.Max(point => point.PressurePsi);
        if (DemandPressurePsi > maxPressure)
        {
            maxPressure = DemandPressurePsi;
        }

        maxFlow = maxFlow <= 0 ? 1 : maxFlow * 1.05;
        maxPressure = maxPressure <= 0 ? 1 : maxPressure * 1.05;

        DrawAxes(margin, width, height, maxFlow, maxPressure);

        PointCollection supplyPoints = new PointCollection();
        foreach (WaterSupplyCurvePoint point in points)
        {
            supplyPoints.Add(ToCanvas(point.FlowGpm, point.PressurePsi, margin, width, height, maxFlow, maxPressure));
        }

        Polyline supplyLine = new Polyline
        {
            Points = supplyPoints,
            Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            StrokeThickness = 2.5
        };
        ChartCanvas.Children.Add(supplyLine);

        if (DemandFlowGpm > 0 && DemandPressurePsi > 0)
        {
            Point demandPoint = ToCanvas(
                DemandFlowGpm,
                DemandPressurePsi,
                margin,
                width,
                height,
                maxFlow,
                maxPressure);

            Ellipse marker = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(marker, demandPoint.X - 6);
            Canvas.SetTop(marker, demandPoint.Y - 6);
            ChartCanvas.Children.Add(marker);
        }
    }

    private static void DrawAxes(double margin, double width, double height, double maxFlow, double maxPressure)
    {
        // Axes are drawn implicitly by chart border; labels rendered via parent module text.
    }

    private static Point ToCanvas(
        double flow,
        double pressure,
        double margin,
        double width,
        double height,
        double maxFlow,
        double maxPressure)
    {
        double x = margin + (flow / maxFlow) * width;
        double y = margin + height - (pressure / maxPressure) * height;
        return new Point(x, y);
    }
}
