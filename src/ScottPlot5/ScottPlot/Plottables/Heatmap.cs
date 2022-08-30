﻿using ScottPlot.Axis;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ScottPlot.Plottables
{
    public class Heatmap : IPlottable
    {
        public bool IsVisible { get; set; } = true;
        public IAxes Axes { get; set; } = Axis.Axes.Default;

        private double[,] _intensities;
        public double[,] Intensities
        {
            get => _intensities;
            set
            {
                _intensities = value;
                Update();
            }
        }

        private SKBitmap? bitmap = null;
        
        public Heatmap(double[,] intensities)
        {
            Intensities = intensities;
        }

        ~Heatmap()
        {
            bitmap?.Dispose();
        }

        private void Update()
        {
            bitmap?.Dispose();
            SKImageInfo imageInfo = new(Intensities.GetLength(0), Intensities.GetLength(1));

            bitmap = new(imageInfo);
            var intensities = Normalize(Intensities.Cast<double>(), null, null);
            double[] foo = intensities.ToArray();


            uint[] rgba = intensities.Select(i => {
                byte alpha = 0xff;
                byte red = (byte)(i * 255);
                byte green = red;
                byte blue = red;
                return (uint)red + (uint)(green << 8) + (uint)(blue << 16) + (uint)(alpha << 24);
            }).ToArray();

            GCHandle handle = GCHandle.Alloc(rgba, GCHandleType.Pinned);
            bitmap.InstallPixels(imageInfo, handle.AddrOfPinnedObject(), imageInfo.RowBytes, (IntPtr _, object _) => handle.Free());
        }

        double Normalize(double intensity, Range range)
        {
            return (intensity - range.Min) / (range.Max - range.Min);
        }

        private IEnumerable<double> Normalize(IEnumerable<double> input, Range? domain, Range? range) // Codomain might be a less repetitive but more obscure alternative?
        {
            domain ??= new(input.Min(), input.Max());
            range ??= new(domain.Value.Min, domain.Value.Max);

            domain = new(Math.Min(domain.Value.Min, range.Value.Min), Math.Max(domain.Value.Max, range.Value.Max));

            Range normalizedRange = new(Normalize(range.Value.Min, domain.Value), Normalize(range.Value.Max, domain.Value));
            return input.AsParallel().AsOrdered().Select(i => normalizedRange.Clamp(Normalize(i, domain.Value)));
        }

        public AxisLimits GetAxisLimits()
        {
            return new(0, bitmap?.Width ?? 1, 0, bitmap?.Height ?? 1);
        }

        public void Render(SKSurface surface)
        {
            if (bitmap is not null)
            {
                SKRect rect = new(Axes.GetPixelX(0), Axes.GetPixelY(bitmap.Height), Axes.GetPixelX(bitmap.Width), Axes.GetPixelY(0));
                surface.Canvas.DrawBitmap(bitmap, rect);
            }
        }
    }
}
