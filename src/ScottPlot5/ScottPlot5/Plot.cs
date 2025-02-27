﻿using ScottPlot.Axis;
using ScottPlot.Rendering;
using ScottPlot.Layouts;
using ScottPlot.Axis.StandardAxes;
using ScottPlot.Legends;
using ScottPlot.Benchmarking;
using ScottPlot.Control;

namespace ScottPlot;

public class Plot : IDisposable
{
    public List<IXAxis> XAxes { get; } = new();
    public List<IYAxis> YAxes { get; } = new();
    public List<IPanel> Panels { get; } = new();
    public Panels.TitlePanel Title { get; } = new();
    public List<IGrid> Grids { get; } = new();
    public List<ILegend> Legends { get; } = new();
    public List<IPlottable> Plottables { get; } = new();
    public AddPlottable Add { get; }
    public IPalette Palette { get => Add.Palette; set => Add.Palette = value; }
    public IRenderer Renderer { get; set; } = new StandardRenderer();
    public ILayoutMaker Layout { get; set; } = new Layouts.StandardLayoutMaker();
    public AutoScaleMargins Margins { get; } = new();
    public Color FigureBackground { get; set; } = Colors.White;
    public Color DataBackground { get; set; } = Colors.White;
    public IBenchmark Benchmark { get; set; } = new StandardBenchmark();
    public IZoomRectangle ZoomRectangle { get; set; }

    /// <summary>
    /// Any state stored across renders can be stored here.
    /// </summary>
    internal RenderDetails LastRenderInfo = new();

    /// <summary>
    /// This property provides access to the primary horizontal axis below the plot.
    /// WARNING: Accessing this property will throw if the first bottom axis is not a standard axis.
    /// </summary>
    public BottomAxis XAxis
    {
        get
        {
            var lowerAxes = XAxes.Where(x => x.Edge == Edge.Bottom);

            if (!lowerAxes.Any())
                throw new InvalidOperationException("Plot does not contain any bottom axes");

            if (lowerAxes.First() is not BottomAxis)
                throw new InvalidOperationException("Primary bottom axis is not a standard bottom axis");

            return (BottomAxis)lowerAxes.First();
        }
    }


    /// <summary>
    /// This property provides access to the primary vertical axis to the left of the plot.
    /// WARNING: Accessing this property will throw if the first bottom axis is not a standard axis.
    /// </summary>
    public LeftAxis YAxis
    {
        get
        {
            var leftAxes = YAxes.Where(x => x.Edge == Edge.Left);

            if (!leftAxes.Any())
                throw new InvalidOperationException("Plot does not contain any left axes");

            if (leftAxes.First() is not LeftAxis)
                throw new InvalidOperationException("Primary left axis is not a standard left axis");

            return (LeftAxis)leftAxes.First();
        }
    }

    public Plot()
    {
        // setup the default primary X and Y axes
        IXAxis xAxisPrimary = new BottomAxis();
        IYAxis yAxisPrimary = new LeftAxis();
        XAxes.Add(xAxisPrimary);
        YAxes.Add(yAxisPrimary);

        // add labeless secondary axes to get right side ticks and padding
        IXAxis xAxisSecondary = new TopAxis();
        IYAxis yAxisSecondary = new RightAxis();
        XAxes.Add(xAxisSecondary);
        YAxes.Add(yAxisSecondary);

        // setup the zoom rectangle to use the primary axes
        ZoomRectangle = new StandardZoomRectangle(xAxisPrimary, yAxisPrimary);

        // add a default grid using the primary axes
        IGrid grid = new Grids.DefaultGrid(xAxisPrimary, yAxisPrimary);
        Grids.Add(grid);

        // add a standard legend
        ILegend legend = new StandardLegend();
        Legends.Add(legend);

        // setup the helper class that creates and adds plottables to this plot
        Add = new(this);
    }

    public void Dispose()
    {
        Plottables.Clear();
        Grids.Clear();
        Panels.Clear();
        YAxes.Clear();
        XAxes.Clear();
    }

    #region Axis Management

    internal IAxis[] GetAllAxes() => XAxes.Select(x => (IAxis)x).Concat(YAxes).ToArray();

    internal IPanel[] GetAllPanels() => XAxes.Select(x => (IPanel)x)
        .Concat(YAxes)
        .Concat(Panels)
        .Concat(new[] { Title })
        .ToArray();

    //[Obsolete("WARNING: NOT ALL LIMITS ARE AFFECTED")]
    public void SetAxisLimits(double left, double right, double bottom, double top)
    {
        XAxis.Min = left;
        XAxis.Max = right;
        YAxis.Min = bottom;
        YAxis.Max = top;
    }

    //[Obsolete("WARNING: NOT ALL LIMITS ARE AFFECTED")]
    public void SetAxisLimits(double? left = null, double? right = null, double? bottom = null, double? top = null)
    {
        XAxis.Min = left ?? XAxis.Min;
        XAxis.Max = right ?? XAxis.Max;
        YAxis.Min = bottom ?? YAxis.Min;
        YAxis.Max = top ?? YAxis.Max;
    }

    //[Obsolete("WARNING: NOT ALL LIMITS ARE AFFECTED")]
    public void SetAxisLimits(CoordinateRect rect)
    {
        SetAxisLimits(rect.XMin, rect.XMax, rect.YMin, rect.YMax);
    }

    //[Obsolete("WARNING: NOT ALL LIMITS ARE AFFECTED")]
    public void SetAxisLimits(AxisLimits rect)
    {
        SetAxisLimits(rect.Rect);
    }

    public AxisLimits GetAxisLimits()
    {
        return new AxisLimits(XAxis.Min, XAxis.Max, YAxis.Min, YAxis.Max);
    }

    public MultiAxisLimits GetMultiAxisLimits()
    {
        MultiAxisLimits limits = new();
        XAxes.ForEach(xAxis => limits.RememberLimits(xAxis, xAxis.Min, xAxis.Max));
        YAxes.ForEach(yAxis => limits.RememberLimits(yAxis, yAxis.Min, yAxis.Max));
        return limits;
    }

    /// <summary>
    /// Automatically scale the axis limits to fit the data.
    /// Note: This used to be AxisAuto().
    /// Note: Margin size can be customized by editing properties of <see cref="Margins"/>
    /// </summary>
    public void AutoScale(bool tight = false)
    {
        // reset limits for all axes
        XAxes.ForEach(xAxis => xAxis.Range.Reset());
        YAxes.ForEach(yAxis => yAxis.Range.Reset());

        // assign default axes to plottables without axes
        Rendering.Common.ReplaceNullAxesWithDefaults(this);

        // expand all axes by the limits of each plot
        foreach (IPlottable plottable in Plottables)
        {
            AutoScale(plottable.Axes.XAxis, plottable.Axes.YAxis, tight);
        }
    }

    /// <summary>
    /// Automatically scale the given axes to fit the data in plottables which use them
    /// </summary>
    public void AutoScale(IXAxis xAxis, IYAxis yAxis, bool tight = false)
    {
        // reset limits only for these axes
        xAxis.Range.Reset();
        yAxis.Range.Reset();

        // assign default axes to plottables without axes
        Rendering.Common.ReplaceNullAxesWithDefaults(this);

        // expand all axes by the limits of each plot
        foreach (IPlottable plottable in Plottables)
        {
            AxisLimits limits = plottable.GetAxisLimits();
            plottable.Axes.YAxis.Range.Expand(limits.Rect.YRange);
            plottable.Axes.XAxis.Range.Expand(limits.Rect.XRange);
        }

        // apply margins
        if (!tight)
        {
            XAxes.ForEach(xAxis => xAxis.Range.ZoomFrac(Margins.ZoomFracX));
            YAxes.ForEach(yAxis => yAxis.Range.ZoomFrac(Margins.ZoomFracY));
        }
    }

    #endregion

    #region Mouse Interaction

    /// <summary>
    /// Apply a click-drag pan operation to the plot
    /// </summary>
    public void MousePan(MultiAxisLimits originalLimits, Pixel mouseDown, Pixel mouseNow)
    {
        float pixelDeltaX = -(mouseNow.X - mouseDown.X);
        float pixelDeltaY = mouseNow.Y - mouseDown.Y;

        // restore mousedown limits
        XAxes.ForEach(xAxis => originalLimits.RestoreLimits(xAxis));
        YAxes.ForEach(yAxis => originalLimits.RestoreLimits(yAxis));

        // pan in the direction opposite of the mouse movement
        XAxes.ForEach(xAxis => xAxis.Range.PanMouse(pixelDeltaX, LastRenderInfo.DataRect.Width));
        YAxes.ForEach(yAxis => yAxis.Range.PanMouse(pixelDeltaY, LastRenderInfo.DataRect.Height));
    }

    /// <summary>
    /// Apply a click-drag zoom operation to the plot
    /// </summary>
    public void MouseZoom(MultiAxisLimits originalLimits, Pixel mouseDown, Pixel mouseNow)
    {
        float pixelDeltaX = mouseNow.X - mouseDown.X;
        float pixelDeltaY = -(mouseNow.Y - mouseDown.Y);

        // restore mousedown limits
        XAxes.ForEach(xAxis => originalLimits.RestoreLimits(xAxis));
        YAxes.ForEach(yAxis => originalLimits.RestoreLimits(yAxis));

        // apply zoom for each axis
        XAxes.ForEach(xAxis => xAxis.Range.ZoomMouseDelta(pixelDeltaX, LastRenderInfo.DataRect.Width));
        YAxes.ForEach(yAxis => yAxis.Range.ZoomMouseDelta(pixelDeltaY, LastRenderInfo.DataRect.Height));
    }

    /// <summary>
    /// Zoom into the coordinate corresponding to the given pixel.
    /// Fractional values >1 zoom in and <1 zoom out.
    /// </summary>
    public void MouseZoom(double fracX, double fracY, Pixel pixel)
    {
        Coordinates mouseCoordinate = GetCoordinate(pixel);
        MultiAxisLimits originalLimits = GetMultiAxisLimits();

        // restore mousedown limits
        XAxes.ForEach(xAxis => originalLimits.RestoreLimits(xAxis));
        YAxes.ForEach(yAxis => originalLimits.RestoreLimits(yAxis));

        // apply zoom for each axis
        XAxes.ForEach(xAxis => xAxis.Range.ZoomFrac(fracX, xAxis.GetCoordinate(pixel.X, LastRenderInfo.DataRect)));
        YAxes.ForEach(yAxis => yAxis.Range.ZoomFrac(fracY, yAxis.GetCoordinate(pixel.Y, LastRenderInfo.DataRect)));
    }

    /// <summary>
    /// Update the shape of the zoom rectangle
    /// </summary>
    /// <param name="mouseDown">Location of the mouse at the start of the drag</param>
    /// <param name="mouseNow">Location of the mouse now (after dragging)</param>
    /// <param name="vSpan">If true, shade the full region between two X positions</param>
    /// <param name="hSpan">If true, shade the full region between two Y positions</param>
    public void MouseZoomRectangle(Pixel mouseDown, Pixel mouseNow, bool vSpan, bool hSpan)
    {
        ZoomRectangle.Update(mouseDown, mouseNow);
        ZoomRectangle.VerticalSpan = vSpan;
        ZoomRectangle.HorizontalSpan = hSpan;
    }

    /// <summary>
    /// Return the pixel for a specific coordinate using measurements from the most recent render.
    /// </summary>
    public Pixel GetPixel(Coordinates coord)
    {
        PixelRect dataRect = LastRenderInfo.DataRect;
        float x = XAxis.GetPixel(coord.X, dataRect);
        float y = YAxis.GetPixel(coord.Y, dataRect);
        return new Pixel(x, y);
    }

    /// <summary>
    /// Return the coordinate for a specific pixel using measurements from the most recent render.
    /// </summary>
    public Coordinates GetCoordinate(Pixel pixel, IXAxis? xAxis = null, IYAxis? yAxis = null)
    {
        // TODO: multi-axis support
        PixelRect dataRect = LastRenderInfo.DataRect;
        double x = XAxis.GetCoordinate(pixel.X, dataRect);
        double y = YAxis.GetCoordinate(pixel.Y, dataRect);
        return new Coordinates(x, y);
    }

    #endregion

    #region Rendering and Image Creation

    public void Render(SKSurface surface)
    {
        LastRenderInfo = Renderer.Render(surface, this);
    }

    public byte[] GetImageBytes(int width, int height, ImageFormat format = ImageFormat.Png, int quality = 100)
    {
        if (width < 1)
            throw new ArgumentException($"{nameof(width)} must be greater than 0");

        if (height < 1)
            throw new ArgumentException($"{nameof(height)} must be greater than 0");

        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        SKSurface surface = SKSurface.Create(info);
        if (surface is null)
            throw new NullReferenceException($"invalid SKImageInfo");
        Render(surface);
        SKImage snap = surface.Snapshot();
        SKEncodedImageFormat skFormat = format.ToSKFormat();
        SKData data = snap.Encode(skFormat, quality);
        byte[] bytes = data.ToArray();
        return bytes;
    }

    public string SaveJpeg(string path, int width, int height, int quality = 85)
    {
        byte[] bytes = GetImageBytes(width, height, ImageFormat.Jpeg, quality);
        File.WriteAllBytes(path, bytes);
        return Path.GetFullPath(path);
    }

    public string SavePng(string path, int width, int height)
    {
        byte[] bytes = GetImageBytes(width, height, ImageFormat.Png, 100);
        File.WriteAllBytes(path, bytes);
        return Path.GetFullPath(path);
    }

    public string SaveBmp(string path, int width, int height)
    {
        byte[] bytes = GetImageBytes(width, height, ImageFormat.Bmp, 100);
        File.WriteAllBytes(path, bytes);
        return Path.GetFullPath(path);
    }

    public string SaveWebp(string path, int width, int height, int quality = 85)
    {
        byte[] bytes = GetImageBytes(width, height, ImageFormat.Webp, quality);
        File.WriteAllBytes(path, bytes);
        return Path.GetFullPath(path);
    }

    #endregion

    #region Shortcuts

    /// <summary>
    /// Clears the <see cref="Plottables"/> list
    /// </summary>
    public void Clear() => Plottables.Clear();

    /// <summary>
    /// Return the first default grid in use.
    /// Throws an exception if no default grids exist.
    /// </summary>
    public Grids.DefaultGrid GetDefaultGrid()
    {
        IEnumerable<Grids.DefaultGrid> defaultGrids = Grids.OfType<Grids.DefaultGrid>();
        if (defaultGrids.Any())
            return defaultGrids.First();
        else
            throw new InvalidOperationException("The plot has no default grids");
    }

    /// <summary>
    /// Return the first default legend in use.
    /// Throws an exception if no default legends exist.
    /// </summary>
    public Legends.StandardLegend GetLegend()
    {
        IEnumerable<Legends.StandardLegend> standardLegends = Legends.OfType<Legends.StandardLegend>();
        if (standardLegends.Any())
            return standardLegends.First();
        else
            throw new InvalidOperationException("The plot has no standard legends");
    }

    #endregion

    #region Developer Tools

    public void Developer_ShowAxisDetails(bool enable = true)
    {
        foreach (IPanel panel in GetAllAxes())
        {
            panel.ShowDebugInformation = enable;
        }
    }

    #endregion
}
