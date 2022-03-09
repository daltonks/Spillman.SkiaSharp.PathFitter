using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Spillman.SkiaSharp
{
    // ReSharper disable once InconsistentNaming
    public static class SKPathExtensions
    {
        public static void AddCubicSegments(this SKPath path, IReadOnlyCollection<CubicBezierSegment> segments, bool closed)
        {
            // ReSharper disable once IntroduceOptionalParameters.Global
            AddCubicSegments(path, segments, closed, moveToFirstPoint: true);
        }

        // Based on the paper.js implementation of Path.drawSegments
        // https://github.com/paperjs/paper.js/blob/develop/src/path/Path.js
        public static void AddCubicSegments(this SKPath path, IReadOnlyCollection<CubicBezierSegment> segments, bool closed, bool moveToFirstPoint)
        {
            if (segments.Count == 1)
            {
                var segment = segments.First();
                if (moveToFirstPoint)
                {
                    path.MoveTo(segment.Point);
                }
                path.LineTo(segment.Point);
                return;
            }

            var first = true;
            var curX = 0f;
            var curY = 0f;
            var prevX = 0f;
            var prevY = 0f;
            var inX = 0f;
            var inY = 0f;
            var outX = 0f;
            var outY = 0f;

            foreach (var segment in segments)
            {
                ApplySegment(segment);
            }
            // Close path by drawing first segment again
            if (closed && segments.Count > 0)
            {
                ApplySegment(segments.First());
            }

            void ApplySegment(CubicBezierSegment segment)
            {
                var point = segment.Point;
                curX = point.X;
                curY = point.Y;
                if (first)
                {
                    if (moveToFirstPoint)
                    {
                        path.MoveTo(curX, curY);
                    }
                    first = false;
                }
                else
                {
                    var handleIn = segment.HandleIn;
                    inX = curX + handleIn.X;
                    inY = curY + handleIn.Y;

                    if (inX == curX && inY == curY
                                    && outX == prevX && outY == prevY)
                    {
                        path.LineTo(curX, curY);
                    }
                    else
                    {
                        path.CubicTo(outX, outY, inX, inY, curX, curY);
                    }
                }

                prevX = curX;
                prevY = curY;

                var handleOut = segment.HandleOut;
                outX = prevX + handleOut.X;
                outY = prevY + handleOut.Y;
            }
        }

        public static List<CubicBezierSegment> GetCubicSegments(this SKPath path)
        {
            var segments = new List<CubicBezierSegment>();

            using (var iterator = path.CreateRawIterator())
            {
                var points = new Span<SKPoint>(new SKPoint[4]);
                CubicBezierSegment previousSegment = null;
                while (true)
                {
                    var verb = iterator.Next(points);
                    
                    switch (verb)
                    {
                        case SKPathVerb.Line:
                            if (previousSegment == null)
                            {
                                previousSegment = new CubicBezierSegment(points[0]);
                                segments.Add(previousSegment);
                            }

                            previousSegment = new CubicBezierSegment(points[1]);
                            segments.Add(previousSegment);

                            break;
                        case SKPathVerb.Cubic:
                            if (previousSegment == null)
                            {
                                previousSegment = new CubicBezierSegment(points[0]);
                                segments.Add(previousSegment);
                            }
                            previousSegment.HandleOut = points[1] - previousSegment.Point;

                            previousSegment = new CubicBezierSegment(points[3]) { HandleIn = points[2] - points[3] };
                            segments.Add(previousSegment);

                            break;
                        case SKPathVerb.Done:
                            return segments;
                        default:
                            previousSegment = null;
                            break;
                    }
                }
            }
        }
    }
}
