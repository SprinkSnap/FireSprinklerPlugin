using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
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

    public static readonly DependencyProperty DemandCurveProperty =
        DependencyProperty.Register(
            nameof(DemandCurve),
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

    public IEnumerable DemandCurve
    {
        get => (IEnumerable)GetValue(DemandCurveProperty);
        set => SetValue(DemandCurveProperty, value);
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
        List<WaterSupplyCurvePoint> supplyPoints = ToPointList(SupplyCurve);
        List<WaterSupplyCurvePoint> demandPoints = ToPointList(DemandCurve);

        if (supplyPoints.Count < 2 || ActualWidth < 40 || ActualHeight < 40)
        {
            return;
        }

        const double margin = 36.0;
        double width = ActualWidth - margin * 2;
        double height = ActualHeight - margin * 2;
        double maxFlow = supplyPoints.Max(point => point.FlowGpm);
        foreach (WaterSupplyCurvePoint point in demandPoints)
        {
            if (point.FlowGpm > maxFlow)
            {
                maxFlow = point.FlowGpm;
            }
        }

        if (DemandFlowGpm > maxFlow)
        {
            maxFlow = DemandFlowGpm;
        }

        double maxPressure = supplyPoints.Max(point => point.PressurePsi);
        foreach (WaterSupplyCurvePoint point in demandPoints)
        {
            if (point.PressurePsi > maxPressure)
            {
                maxPressure = point.PressurePsi;
            }
        }

        if (DemandPressurePsi > maxPressure)
        {
            maxPressure = DemandPressurePsi;
        }

        maxFlow = maxFlow <= 0 ? 1 : maxFlow * 1.05;
        maxPressure = maxPressure <= 0 ? 1 : maxPressure * 1.05;
        double maxScaledFlow = Nfpa13HydraulicGraphCalculator.ScaleFlowForGraph(maxFlow);

        DrawSupplyLine(supplyPoints, margin, width, height, maxScaledFlow, maxPressure);
        DrawDemandLine(demandPoints, margin, width, height, maxScaledFlow, maxPressure);

        if (demandPoints.Count == 0 && DemandFlowGpm > 0 && DemandPressurePsi > 0)
        {
            DrawDemandMarker(DemandFlowGpm, DemandPressurePsi, margin, width, height, maxScaledFlow, maxPressure);
        }
    }

    private static List<WaterSupplyCurvePoint> ToPointList(IEnumerable points)
    {
        return (points as IEnumerable<WaterSupplyCurvePoint>)?.ToList()
            ?? points?.Cast<WaterSupplyCurvePoint>().ToList()
            ?? new List<WaterSupplyCurvePoint>();
    }

    private void DrawSupplyLine(
        IList<WaterSupplyCurvePoint> points,
        double margin,
        double width,
        double height,
        double maxScaledFlow,
        double maxPressure)
    {
        PointCollection supplyPoints = new PointCollection();
        foreach (WaterSupplyCurvePoint point in points)
        {
            supplyPoints.Add(ToCanvas(point.FlowGpm, point.PressurePsi, margin, width, height, maxScaledFlow, maxPressure));
        }

        Polyline supplyLine = new Polyline
        {
            Points = supplyPoints,
            Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            StrokeThickness = 2.5
        };
        ChartCanvas.Children.Add(supplyLine);
    }

    private void DrawDemandLine(
        IList<WaterSupplyCurvePoint> points,
        double margin,
        double width,
        double height,
        double maxScaledFlow,
        double maxPressure)
    {
        if (points.Count >= 2)
        {
            PointCollection demandLinePoints = new PointCollection();
            foreach (WaterSupplyCurvePoint point in points)
            {
                demandLinePoints.Add(ToCanvas(point.FlowGpm, point.PressurePsi, margin, width, height, maxScaledFlow, maxPressure));
            }

            Polyline demandLine = new Polyline
            {
                Points = demandLinePoints,
                Stroke = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                StrokeThickness = 2.5
            };
            ChartCanvas.Children.Add(demandLine);
        }

        foreach (WaterSupplyCurvePoint point in points)
        {
            DrawDemandMarker(point.FlowGpm, point.PressurePsi, margin, width, height, maxScaledFlow, maxPressure);
        }
    }

    private void DrawDemandMarker(
        double flowGpm,
        double pressurePsi,
        double margin,
        double width,
        double height,
        double maxScaledFlow,
        double maxPressure)
    {
        Point demandPoint = ToCanvas(flowGpm, pressurePsi, margin, width, height, maxScaledFlow, maxPressure);
        Ellipse marker = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            Stroke = Brushes.White,
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(marker, demandPoint.X - 5);
        Canvas.SetTop(marker, demandPoint.Y - 5);
        ChartCanvas.Children.Add(marker);
    }

    private static Point ToCanvas(
        double flow,
        double pressure,
        double margin,
        double width,
        double height,
        double maxScaledFlow,
        double maxPressure)
    {
        double scaledFlow = Nfpa13HydraulicGraphCalculator.ScaleFlowForGraph(flow);
        double x = margin + (scaledFlow / maxScaledFlow) * width;
        double y = margin + height - (pressure / maxPressure) * height;
        return new Point(x, y);
    }
}
