using SkiaSharp;

namespace Spillman.SkiaSharp
{
    public class CubicBezierSegment
    {
        public SKPoint Point { get; set; }
        public SKPoint HandleIn { get; set; }
        public SKPoint HandleOut { get; set; }

        public CubicBezierSegment(SKPoint point)
        {
            Point = point;
        }

        public CubicBezierSegment(SKPoint point, SKPoint handleIn, SKPoint handleOut)
        {
            Point = point;
            HandleIn = handleIn;
            HandleOut = handleOut;
        }
    }
}