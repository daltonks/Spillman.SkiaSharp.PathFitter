# Spillman.SkiaSharp.PathFitter

Recreation of the [Path.simplify() method from paper.js](http://paperjs.org/examples/path-simplification/) for SkiaSharp

[NuGet package](https://www.nuget.org/packages/Spillman.SkiaSharp.PathFitter)

## Example
```cs
// Calculate CubicBezierSegments from SKPoints
var points = new [] { new SKPoint(0, 1), new SKPoint (2, 3), new SKPoint(4, 5) };
var isClosed = false;
var error = 2.5;

var pathFitter = new PathFitter(points, isClosed);
var segments = pathFitter.Fit(error);

// Apply CubicBezierSegments to an SKPath
var path = new SKPath();
path.AddCubicSegments(segments, isClosed);

// Extract CubicBezierSegments from an SKPath
segments = path.GetCubicSegments();
```
