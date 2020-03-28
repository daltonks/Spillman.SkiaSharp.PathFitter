using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Spillman.SkiaSharp
{
    // Based on the paper.js implementation of PathFitter
    // https://github.com/paperjs/paper.js/blob/develop/src/path/PathFitter.js
    public class PathFitter
    {
        private readonly List<SKPoint> _points = new List<SKPoint>();
        private readonly bool _closed;

        public PathFitter(IReadOnlyCollection<SKPoint> points, bool closed)
        {
            if (!points.Any())
            {
                return;
            }

            // Copy over points from path and filter out adjacent duplicates.
            SKPoint? prev = null;
            foreach (var point in points)
            {
                if (prev == null || prev.Value != point)
                {
                    _points.Add(point);
                    prev = point;
                }
            }

            // We need to duplicate the first and last segment when simplifying a
            // closed path.
            if (closed)
            {
                _points.Insert(0, _points[points.Count - 1]);
                _points.Add(_points[1]); // The point previously at index 0 is now 1.
            }
            _closed = closed;
        }

        public List<CubicBezierSegment> Fit(double error = 2.5)
        {
            if (!_points.Any())
            {
                return new List<CubicBezierSegment>();
            }

            var segments = new List<CubicBezierSegment>
            {
                new CubicBezierSegment(_points.First())
            };

            if (_points.Count == 1)
            {
                return segments;
            }

            // To support reducing paths with multiple points in the same place
            // to one segment:
            FitCubic(
                segments, 
                error, 
                0, _points.Count - 1, 
                _points[1].Subtract(_points[0]), // Left Tangent
                _points[_points.Count - 2].Subtract(_points[_points.Count - 1]) // Right Tangent
            );

            // Remove the duplicated segments for closed paths again.
            if (_closed)
            {
                segments.RemoveAt(0);
                segments.RemoveAt(segments.Count - 1);
            }

            return segments;
        }

        private void FitCubic(List<CubicBezierSegment> segments, double error, int first, int last, SKPoint tan1, SKPoint tan2)
        {
            //  Use heuristic if region only has two points in it
            if (last - first == 1)
            {
                var pt1 = _points[first];
                var pt2 = _points[last];
                var dist = pt1.Distance(pt2) / 3;
                AddCurve(
                    segments, 
                    new[]
                    {
                        pt1, 
                        pt1.Add(tan1.Normalize(dist)), 
                        pt2.Add(tan2.Normalize(dist)), 
                        pt2
                    }
                );
                return;
            }

            // Parameterize points, and attempt to fit curve
            var uPrime = ChordLengthParameterize(first, last);
            var maxError = Math.Max(error, error * error);
            var split = 0;
            var parametersInOrder = true;

            // Try 4 iterations
            for (var i = 0; i <= 4; i++)
            {
                var curve = GenerateBezier(first, last, uPrime, tan1, tan2);
                //  Find max deviation of points to fitted curve
                var max = FindMaxError(first, last, curve, uPrime);
                if (max.error < error && parametersInOrder) {
                    AddCurve(segments, curve);
                    return;
                }
                split = max.index;
                // If error not too large, try reparameterization and iteration
                if (max.error >= maxError)
                    break;
                parametersInOrder = Reparameterize(first, last, uPrime, curve);
                maxError = max.error;
            }

            // Fitting failed -- split at max error point and fit recursively
            var tanCenter = _points[split - 1].Subtract(_points[split + 1]);
            FitCubic(segments, error, first, split, tan1, tanCenter);
            FitCubic(segments, error, split, last, tanCenter.Multiply(-1), tan2);
        }

        private void AddCurve(List<CubicBezierSegment> segments, SKPoint[] curve)
        {
            var prev = segments[segments.Count - 1];
            prev.HandleOut = curve[1].Subtract(curve[0]);
            segments.Add(new CubicBezierSegment(curve[3]) { HandleIn = curve[2].Subtract(curve[3]) });
        }

        private SKPoint[] GenerateBezier(int first, int last, double[] uPrime, SKPoint tan1, SKPoint tan2)
        {
            var epsilon = double.Epsilon;
            var pt1 = _points[first];
            var pt2 = _points[last];
            // Create the C and X matrices
            var c = new double[2, 2];
            var x = new double[2];

            var l = last - first + 1;
            for (var i = 0; i < l; i++)
            {
                var u = uPrime[i];
                var t = 1 - u;
                var b = 3 * u * t;
                var b0 = t * t * t;
                var b1 = b * t;
                var b2 = b * u;
                var b3 = u * u * u;
                var a1 = tan1.Normalize(b1);
                var a2 = tan2.Normalize(b2);
                var tmp = _points[first + i]
                    .Subtract(pt1.Multiply(b0 + b1))
                    .Subtract(pt2.Multiply(b2 + b3));
                c[0, 0] += a1.Dot(a1);
                c[0, 1] += a1.Dot(a2);
                // C[1][0] += a1.dot(a2);
                c[1, 0] = c[0, 1];
                c[1, 1] += a2.Dot(a2);
                x[0] += a1.Dot(tmp);
                x[1] += a2.Dot(tmp);
            }

            // Compute the determinants of C and X
            var detC0C1 = c[0, 0] * c[1, 1] - c[1, 0] * c[0, 1];
            double alpha1;
            double alpha2;
            if (Math.Abs(detC0C1) > epsilon)
            {
                // Kramer's rule
                var detC0X = c[0, 0] * x[1]    - c[1, 0] * x[0];
                var detXC1 = x[0]    * c[1, 1] - x[1]    * c[0, 1];
                // Derive alpha values
                alpha1 = detXC1 / detC0C1;
                alpha2 = detC0X / detC0C1;
            } 
            else 
            {
                // Matrix is under-determined, try assuming alpha1 == alpha2
                var c0 = c[0, 0] + c[0, 1];
                var c1 = c[1, 0] + c[1, 1];
                alpha1 = alpha2 = Math.Abs(c0) > epsilon ? x[0] / c0
                                : Math.Abs(c1) > epsilon ? x[1] / c1
                                : 0;
            }

            // If alpha negative, use the Wu/Barsky heuristic (see text)
            // (if alpha is 0, you get coincident control points that lead to
            // divide by zero in any subsequent NewtonRaphsonRootFind() call.
            var segLength = pt2.Distance(pt1);
            var eps = epsilon * segLength;
            SKPoint? handle1 = null;
            SKPoint? handle2 = null;

            if (alpha1 < eps || alpha2 < eps) {
                // fall back on standard (probably inaccurate) formula,
                // and subdivide further if needed.
                alpha1 = alpha2 = segLength / 3;
            }
            else
            {
                // Check if the found control points are in the right order when
                // projected onto the line through pt1 and pt2.
                var line = pt2.Subtract(pt1);
                // Control points 1 and 2 are positioned an alpha distance out
                // on the tangent vectors, left and right, respectively
                handle1 = tan1.Normalize(alpha1);
                handle2 = tan2.Normalize(alpha2);
                if (handle1.Value.Dot(line) - handle2.Value.Dot(line) > segLength * segLength) {
                    // Fall back to the Wu/Barsky heuristic above.
                    alpha1 = alpha2 = segLength / 3;
                    handle1 = handle2 = null; // Force recalculation
                }
            }

            // First and last control points of the Bezier curve are
            // positioned exactly at the first and last data points
            return new []
            {
                pt1,
                pt1.Add(handle1 ?? tan1.Normalize(alpha1)),
                pt2.Add(handle2 ?? tan2.Normalize(alpha2)),
                pt2
            };
        }

        // Given set of points and their parameterization, try to find
        // a better parameterization.
        private bool Reparameterize(int first, int last, double[] u, SKPoint[] curve) {
            for (var i = first; i <= last; i++) {
                u[i - first] = FindRoot(curve, _points[i], u[i - first]);
            }
            // Detect if the new parameterization has reordered the points.
            // In that case, we would fit the points of the path in the wrong order.
            var l = u.Length;
            for (var i = 1; i < l; i++) {
                if (u[i] <= u[i - 1])
                    return false;
            }
            return true;
        }

        // Use Newton-Raphson iteration to find better root.
        private double FindRoot(SKPoint[] curve, SKPoint point, double u)
        {
            var curve1 = new SKPoint[3];
            var curve2 = new SKPoint[2];
            // Generate control vertices for Q'
            for (var i = 0; i <= 2; i++) {
                curve1[i] = curve[i + 1].Subtract(curve[i]).Multiply(3);
            }
            // Generate control vertices for Q''
            for (var i = 0; i <= 1; i++) {
                curve2[i] = curve1[i + 1].Subtract(curve1[i]).Multiply(2);
            }
            // Compute Q(u), Q'(u) and Q''(u)
            var pt = Evaluate(3, curve, u);
            var pt1 = Evaluate(2, curve1, u);
            var pt2 = Evaluate(1, curve2, u);
            var diff = pt.Subtract(point);
            var df = pt1.Dot(pt1) + diff.Dot(pt2);
            // u = u - f(u) / f'(u)
            return IsMachineZero(df) ? u : u - diff.Dot(pt1) / df;
        }

        // TODO: I hope this is the same in C# as it is in javascript x.x
        private const double MachineEpsilon = 1.12e-16;
        private bool IsMachineZero(double val)
        {
            return val >= -MachineEpsilon && val <= MachineEpsilon;
        }

        // Evaluate a bezier curve at a particular parameter value
        private SKPoint Evaluate(int degree, SKPoint[] curve, double t) {
            // Copy array
            var tmp = new SKPoint[curve.Length];
            Array.Copy(curve, tmp, curve.Length);
            // Triangle computation
            for (var i = 1; i <= degree; i++) {
                for (var j = 0; j <= degree - i; j++) {
                    tmp[j] = tmp[j].Multiply(1 - t).Add(tmp[j + 1].Multiply(t));
                }
            }
            return tmp[0];
        }

        // Assign parameter values to digitized points
        // using relative distances between points.
        private double[] ChordLengthParameterize(int first, int last)
        {
            var u = new double[last - first + 1];
            for (var i = first + 1; i <= last; i++)
            {
                u[i - first] = u[i - first - 1] + _points[i].Distance(_points[i - 1]);
            }
            var m = last - first;
            for (var i = 1; i <= m; i++)
            {
                u[i] /= u[m];
            }
            return u;
        }

        // Find the maximum squared distance of digitized points to fitted curve.
        private (double error, int index) FindMaxError(int first, int last, SKPoint[] curve, double[] u)
        {
            var index = (int) Math.Floor((last - first + 1) / 2.0);
            var maxDist = 0.0;
            for (var i = first + 1; i < last; i++) {
                var p = Evaluate(3, curve, u[i - first]);
                var v = p.Subtract(_points[i]);
                var dist = v.X * v.X + v.Y * v.Y; // squared
                if (dist >= maxDist) {
                    maxDist = dist;
                    index = i;
                }
            }

            return (maxDist, index);
        }
    }
}
