using System;
using SkiaSharp;

namespace Spillman.SkiaSharp
{
    // ReSharper disable once InconsistentNaming
    internal static class SKPointExtensions
    {
        public static SKPoint Add(this SKPoint p1, SKPoint p2)
        {
            return p1 + p2;
        }

        public static SKPoint Subtract(this SKPoint p1, SKPoint p2)
        {
            return p1 - p2;
        }

        public static SKPoint Multiply(this SKPoint point, double scalar)
        {
            return new SKPoint((float) (point.X * scalar), (float) (point.Y * scalar));
        }

        public static double Dot(this SKPoint p1, SKPoint p2)
        {
            return p1.X * p2.X + p1.Y * p2.Y;
        }

        public static double GetLength(this SKPoint point)
        {
            return Math.Sqrt(point.X * point.X + point.Y * point.Y);
        }

        public static double Distance(this SKPoint p1, SKPoint p2)
        {
            var xDif = p1.X - p2.X;
            var yDif = p1.Y - p2.Y;
            return Math.Sqrt(xDif * xDif + yDif * yDif);
        }

        public static SKPoint Normalize(this SKPoint point, double length)
        {
            var current = point.GetLength();
            var scale = current != 0 ? length / current : 0;
            point = new SKPoint((float) (point.X * scale), (float) (point.Y * scale));
            return point;
        }
    }
}