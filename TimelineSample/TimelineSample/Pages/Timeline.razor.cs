using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Blazor;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.VisualElements;
using TimelineSample.Shared;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;

namespace TimelineSample.Pages
{
    public class Sample
    {
        public enum TestStatus
        {
            [StringValue("成功")]
            Success,
            [StringValue("警告")]
            Warning,
            [StringValue("エラー")]
            Error,
        }

        public enum TestCase
        {
            [StringValue("A")]
            A,
            [StringValue("B")]
            B,
            [StringValue("C")]
            C,
            [StringValue("D")]
            D,
            [StringValue("E")]
            E,
            [StringValue("F")]
            F
        }

        public static (SolidColorPaint?, SolidColorPaint?) FillAndStroke(TestStatus status, int zIndex = 0) => status switch
        {
            TestStatus.Success => (new SolidColorPaint(SKColors.White) { ZIndex = zIndex + 1, }, new SolidColorPaint(new SKColor(40, 167, 69)) { ZIndex = zIndex + 2, StrokeThickness = 5 }),
            TestStatus.Warning => (new SolidColorPaint(SKColors.White) { ZIndex = zIndex + 1, }, new SolidColorPaint(new SKColor(255, 193, 7)) { ZIndex = zIndex + 2, StrokeThickness = 5 }),
            TestStatus.Error => (new SolidColorPaint(SKColors.White) { ZIndex = zIndex + 1, }, new SolidColorPaint(new SKColor(220, 53, 69)) { ZIndex = zIndex + 2, StrokeThickness = 5 }),
            _ => (null, null)
        };

        public DateTime Date { get; set; }

        public int Id { get; set; }

        public TestCase Case { get; set; }

        public TestStatus Status { get; set; }
    };

    internal class PlotData<T> where T : new()
    {
        public T Data { get; set; } = new();

        public bool IsHoverOn { get; set; } = false;
    };

    public partial class Timeline
    {
        [Inject]
        public HttpClient? Http { get; set; }

        [Parameter]
        public string Title { get; set; } = string.Empty;

        [Parameter]
        public EventCallback<int> OnPlotPointClicked { get; set; }

        public LabelVisual? ChartTitle { get; set; }

        private ObservableCollection<ISeries> chartSeries = new();

        private CustomLegend chartLegend = new();

        private CustomTooltip chartTooltip = new();

        private CartesianChart? chart = null!;

        private List<Axis>? XAxes { get; set; }

        private List<Axis>? YAxes { get; set; }

        public RectangularSection[]? Sections { get; set; }

        private PlotData<Sample>? selectedSample;

        private int TestsetCount { get; set; } = 32;

        private void OnTestsetCountChanged()
        {
            GeneratePlotData();
            ResetZoom();
        }

        private Random gen = new Random();

        private LineSeries<PlotData<Sample>> SetupSeriese(PlotData<Sample>[] dataset, string name)
        {
            var series = new LineSeries<PlotData<Sample>>
            {
                Values = dataset.Where(x => x.Data.Case.GetStringValue() == name).OrderBy(x => x.Data.Date).ToArray(),
                Mapping = (x, index) => new(x.Data.Date.ToOADate(), (int)x.Data.Case),
                GeometrySize = 25,
                Fill = null,
            };

            series.PointCreated += OnPointCreated;
            series.ChartPointPointerDown += OnPointerDown;
            series.ChartPointPointerHover += OnPointerHover;
            series.ChartPointPointerHoverLost += OnPointerHoverLost;

            return series;
        }

        private void OnPointCreated(ChartPoint<PlotData<Sample>, CircleGeometry, LabelGeometry> point)
        {
            if (point?.Visual is null || point?.Model is null)
                return;

            (point.Visual.Fill, point.Visual.Stroke) = Sample.FillAndStroke(point.Model.Data.Status);
        }

        private void OnPointerDown(IChartView chart, ChartPoint<PlotData<Sample>, CircleGeometry, LabelGeometry>? point)
        {
            if (point?.Visual is null || point?.Model is null)
                return;

            if (point.Model.IsHoverOn == false)
                return;

            selectedSample = point.Model;
            OnPlotPointClicked.InvokeAsync(point.Model.Data.Id);

            Sections = new RectangularSection[]
            {
                new RectangularSection
                {
                    Xi = selectedSample.Data.Date.ToOADate(),
                    Xj = selectedSample.Data.Date.ToOADate(),
                    Stroke = new SolidColorPaint
                    {
                        Color = SKColors.Red,
                        StrokeThickness = 3,
                        PathEffect = new DashEffect(new float[] { 6, 6 })
                    }
                },
            };

            point.Visual.Fill = new SolidColorPaint(new SKColor(0, 123, 255));
            chart.Invalidate(); // <- ensures the canvas is redrawn after we set the fill
        }

        private void OnPointerHover(IChartView chart, ChartPoint<PlotData<Sample>, CircleGeometry, LabelGeometry>? point)
        {
            if (point?.Visual is null || point?.Model is null)
                return;

            point.Model.IsHoverOn = true;

            // emphasize by filling
            point.Visual.Fill = new SolidColorPaint(SKColors.Yellow);
            chart.Invalidate();
        }

        private void OnPointerHoverLost(IChartView chart, ChartPoint<PlotData<Sample>, CircleGeometry, LabelGeometry>? point)
        {
            if (point?.Visual is null || point?.Model is null)
                return;

            point.Model.IsHoverOn = false;

            // revert fill and stroke
            (point.Visual.Fill, point.Visual.Stroke) = Sample.FillAndStroke(point.Model.Data.Status);
            chart.Invalidate();
        }

        DateTime RandomDay()
        {
            DateTime start = new DateTime(2024, 1, 1, 0, 0, 0);
            int range = (DateTime.Today - start).Days;
            return start.AddDays(gen.Next(range));
        }

        private T RandomEnum<T>(T[] vals)
        {
            return vals[gen.Next(vals.Length)];
        }

        private void GeneratePlotData()
        {
            var testset = new PlotData<Sample>[TestsetCount];
            for (var i = 0; i < TestsetCount; ++i)
            {
                testset[i] = new PlotData<Sample>
                {
                    Data = new Sample()
                    {
                        Date = RandomDay(),
                        Id = i,
                        Case = RandomEnum((Sample.TestCase[])Enum.GetValues(typeof(Sample.TestCase))),
                        Status = RandomEnum((Sample.TestStatus[])Enum.GetValues(typeof(Sample.TestStatus))),
                    }
                };
            }

            selectedSample = null;
            Sections = null;
            chartSeries.Clear();
            foreach (Sample.TestCase val in Enum.GetValues(typeof(Sample.TestCase)))
            {
                chartSeries.Add(SetupSeriese(testset, val.GetStringValue() ?? string.Empty));
            }
        }

        private void PanTo(double? start, double? end)
        {
            var x = XAxes?.First();
            if (x is null)
                return;

            x.MinLimit = start;
            x.MaxLimit = end;
        }

        private void ResetZoom()
        {
            PanTo(null, null);
        }

        private void ZoomOn(double scale)
        {
            if (selectedSample is null)
                return;

            var x = XAxes?.First();
            if (x is null)
                return;

            var range = 0.0;
            if (x.MinLimit is null || x.MaxLimit is null)
            {
                range = (double)(x.VisibleDataBounds.Max - x.VisibleDataBounds.Min) * scale * 0.5;

            }
            else
            {
                range = (double)(x.MaxLimit - x.MinLimit) * scale * 0.5;
            }

            var dateInValue = selectedSample.Data.Date.ToOADate();
            PanTo(dateInValue - range, dateInValue + range);
        }

        private void ZoomInOn()
        {
            ZoomOn(0.5);
        }

        private void ZoomOutOn()
        {
            ZoomOn(2);
        }

        private void SetupAxes()
        {
            {
                Func<double, string> labeler = date => string.Format("{0:yy/MM/dd}", System.DateTime.FromOADate(date));

                var xAxis = new Axis()
                {
                    Labeler = labeler,
                    LabelsRotation = -20,
                    TextSize = 10,
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
                    {
                        StrokeThickness = 1,
                        PathEffect = new DashEffect(new float[] { 3, 3 })
                    }
                };

                XAxes = new List<Axis> { xAxis };
            }

            {
                Func<double, string> labeler = c => { var e = (Sample.TestCase)(Enum.ToObject(typeof(Sample.TestCase), (int)c)); return e.GetStringValue() ?? string.Empty; };
                var yAxis = new Axis()
                {
                    Labeler = labeler,
                    MinStep = 1,
                    MaxLimit = 5.5,
                    MinLimit = -0.5,
                    IsInverted = true,
                };

                YAxes = new List<Axis> { yAxis };
            }
        }

        protected override void OnInitialized()
        {
            ChartTitle = new LabelVisual
            {
                Text = Title,
                TextSize = 25,
                Padding = new LiveChartsCore.Drawing.Padding(15),
                Paint = new SolidColorPaint(SKColors.DarkSlateGray)
            };

            SetupAxes();

            GeneratePlotData();
        }
    }

    public class CustomTooltip : IChartTooltip<SkiaSharpDrawingContext>
    {
        private StackPanel<RoundedRectangleGeometry, SkiaSharpDrawingContext>? _stackPanel;
        private static readonly int s_zIndex = 10100;
        private readonly SolidColorPaint _backgroundPaint = new(new SKColor(28, 49, 58)) { ZIndex = s_zIndex };
        private readonly SolidColorPaint _fontPaint = new(new SKColor(230, 230, 230)) { ZIndex = s_zIndex + 1 };

        public void Show(IEnumerable<ChartPoint> foundPoints, Chart<SkiaSharpDrawingContext> chart)
        {
            if (_stackPanel is null)
            {
                _stackPanel = new StackPanel<RoundedRectangleGeometry, SkiaSharpDrawingContext>
                {
                    Padding = new Padding(5),
                    Orientation = ContainerOrientation.Vertical,
                    HorizontalAlignment = LiveChartsCore.Drawing.Align.Start,
                    VerticalAlignment = LiveChartsCore.Drawing.Align.Middle,
                    BackgroundPaint = _backgroundPaint
                };
            }

            // clear the previous elements.
            foreach (var child in _stackPanel.Children.ToArray())
            {
                _ = _stackPanel.Children.Remove(child);
                chart.RemoveVisual(child);
            }

            foreach (var point in foundPoints)
            {
                if (point is null || point.Context is null || point.Context.DataSource is null)
                    continue;

                var data = (PlotData<Sample>)point.Context.DataSource;

                {
                    var label = new LabelVisual
                    {
                        Text = data.Data.Date.ToString("yyyy/MM/dd HH:mm:ss"), //point.Coordinate.PrimaryValue.ToString("C2"),
                        Paint = _fontPaint,
                        TextSize = 15,
                        Padding = new Padding(8, 0, 0, 0),
                        ClippingMode = ClipMode.None, // required on tooltips 
                        VerticalAlignment = LiveChartsCore.Drawing.Align.Start,
                        HorizontalAlignment = LiveChartsCore.Drawing.Align.Start
                    };

                    var sp = new StackPanel<RoundedRectangleGeometry, SkiaSharpDrawingContext>
                    {
                        Padding = new Padding(0, 4),
                        VerticalAlignment = LiveChartsCore.Drawing.Align.Start,
                        HorizontalAlignment = LiveChartsCore.Drawing.Align.Start,
                        Children =
                        {
                            label
                        }
                    };

                    _stackPanel?.Children.Add(sp);
                }

                {
                    var label = new LabelVisual
                    {
                        Text = $"{data.Data.Id}({data.Data.Status.ToString()})",
                        Paint = _fontPaint,
                        TextSize = 15,
                        Padding = new Padding(8, 0, 0, 0),
                        ClippingMode = ClipMode.None, // required on tooltips 
                        VerticalAlignment = LiveChartsCore.Drawing.Align.Start,
                        HorizontalAlignment = LiveChartsCore.Drawing.Align.Start
                    };

                    var visual = new SVGVisual
                    {
                        Path = SKPath.ParseSvgPathData(SVGPoints.Circle),
                        LocationUnit = MeasureUnit.Pixels,
                        Width = 20,
                        Height = 20,
                        ClippingMode = ClipMode.None,
                    };
                    (visual.Fill, visual.Stroke) = Sample.FillAndStroke(data.Data.Status, s_zIndex);

                    var sp = new StackPanel<RoundedRectangleGeometry, SkiaSharpDrawingContext>
                    {
                        Padding = new Padding(0, 4),
                        VerticalAlignment = LiveChartsCore.Drawing.Align.Start,
                        HorizontalAlignment = LiveChartsCore.Drawing.Align.Start,
                        Children =
                        {
                            visual,
                            label
                        }
                    };

                    _stackPanel?.Children.Add(sp);
                }
            }

            var size = _stackPanel?.Measure(chart);
            if (size is null)
                return;

            var location = foundPoints.GetTooltipLocation((LvcSize)size, chart);

            _stackPanel.X = location.X;
            _stackPanel.Y = location.Y;

            chart.AddVisual(_stackPanel);
        }

        public void Hide(Chart<SkiaSharpDrawingContext> chart)
        {
            if (chart is null || _stackPanel is null)
                return;
            chart.RemoveVisual(_stackPanel);
        }
    }

    public class CustomLegend : IChartLegend<SkiaSharpDrawingContext>
    {
        private static readonly int s_zIndex = 10050;
        private readonly StackPanel<RoundedRectangleGeometry, SkiaSharpDrawingContext> _stackPanel = new();
        private readonly SolidColorPaint _fontPaint = new(new SKColor(30, 20, 30))
        {
            ZIndex = s_zIndex + 1
        };

        public void Draw(Chart<SkiaSharpDrawingContext> chart)
        {
            var legendPosition = chart.GetLegendPosition();

            _stackPanel.X = legendPosition.X;
            _stackPanel.Y = legendPosition.Y;

            chart.AddVisual(_stackPanel);
            if (chart.LegendPosition == LegendPosition.Hidden)
                chart.RemoveVisual(_stackPanel);
        }

        public LvcSize Measure(Chart<SkiaSharpDrawingContext> chart)
        {
            _stackPanel.Orientation = ContainerOrientation.Horizontal;
            _stackPanel.MaxWidth = chart.ControlSize.Width;
            _stackPanel.MaxHeight = chart.ControlSize.Height;

            // clear the previous elements.
            foreach (var visual in _stackPanel.Children.ToArray())
            {
                _ = _stackPanel.Children.Remove(visual);
                chart.RemoveVisual(visual);
            }

            foreach (Sample.TestStatus status in Enum.GetValues(typeof(Sample.TestStatus)))
            {
                var icon = new SVGVisual
                {
                    Path = SKPath.ParseSvgPathData(SVGPoints.Circle),
                    Width = 25,
                    Height = 25,
                    ClippingMode = ClipMode.None,
                };

                (icon.Fill, icon.Stroke) = Sample.FillAndStroke(status);

                var panel = new StackPanel<RectangleGeometry, SkiaSharpDrawingContext>
                {
                    Padding = new Padding(12, 6),
                    VerticalAlignment = LiveChartsCore.Drawing.Align.Middle,
                    HorizontalAlignment = LiveChartsCore.Drawing.Align.Middle,
                    Children =
                {
                    icon,
                    new LabelVisual
                    {
                        Text = status.ToString(),
                        Paint = _fontPaint,
                        TextSize = 15,
                        ClippingMode = ClipMode.None, // required on legends 
                        Padding = new Padding(8, 0, 0, 0),
                        VerticalAlignment = LiveChartsCore.Drawing.Align.Start,
                        HorizontalAlignment = LiveChartsCore.Drawing.Align.Start
                    }
                }
                };

                _stackPanel.Children.Add(panel);
            }

            return _stackPanel.Measure(chart);
        }
    }
}
