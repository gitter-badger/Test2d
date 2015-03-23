﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Test.Core;

namespace Test
{
    public class WpfRenderer : XObject, IRenderer
    {
        private bool _drawPoints;

        public bool DrawPoints
        {
            get { return _drawPoints; }
            set
            {
                if (value != _drawPoints)
                {
                    _drawPoints = value;
                    Notify("DrawPoints");
                }
            }
        }

        private IDictionary<XStyle, Tuple<Brush, Pen>> _styleCache;
        private readonly bool _enableStyleCache = true;

        private IDictionary<XBezier, PathGeometry> _bezierCache;
        private readonly bool _enableBezierCache = true;

        private IDictionary<XQBezier, PathGeometry> _qbezierCache;
        private readonly bool _enableQBezierCache = true;

        public WpfRenderer()
        {
            ClearCache();
        }

        public void ClearCache()
        {
            _styleCache = new Dictionary<XStyle, Tuple<Brush, Pen>>();
            _bezierCache = new Dictionary<XBezier, PathGeometry>();
            _qbezierCache = new Dictionary<XQBezier, PathGeometry>();
        }

        private Brush CreateBrush(XColor color)
        {
            var brush = new SolidColorBrush(
                Color.FromArgb(
                    color.A,
                    color.R,
                    color.G,
                    color.B));
            brush.Freeze();
            return brush;
        }

        private Pen CreatePen(XColor color, double thickness)
        {
            var brush = CreateBrush(color);
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            return pen;
        }

        private Rect CreateRect(XPoint topLeft, XPoint bottomRight, double dx, double dy)
        {
            double tlx = Math.Min(topLeft.X, bottomRight.X);
            double tly = Math.Min(topLeft.Y, bottomRight.Y);
            double brx = Math.Max(topLeft.X, bottomRight.X);
            double bry = Math.Max(topLeft.Y, bottomRight.Y);
            return new Rect(
                new Point(tlx + dx, tly + dy),
                new Point(brx + dx, bry + dy));
        }

        public void Render(object dc, ILayer layer)
        {
            var _dc = dc as DrawingContext;

            foreach (var shape in layer.Shapes)
            {
                shape.Draw(_dc, this, 0, 0);
            }
        }

        public void Draw(object dc, XLine line, double dx, double dy)
        {
            var _dc = dc as DrawingContext;

            Tuple<Brush, Pen> cache;
            Brush fill;
            Pen stroke;
            if (_enableStyleCache && _styleCache.TryGetValue(line.Style, out cache))
            {
                fill = cache.Item1;
                stroke = cache.Item2;
            }
            else
            {
                fill = CreateBrush(line.Style.Fill);
                stroke = CreatePen(
                    line.Style.Stroke,
                    line.Style.Thickness);

                if (_enableStyleCache)
                    _styleCache.Add(line.Style, Tuple.Create(fill, stroke));
            }

            _dc.DrawLine(
                stroke,
                new Point(line.Start.X + dx, line.Start.Y + dy),
                new Point(line.End.X + dx, line.End.Y + dy));
        }

        public void Draw(object dc, XRectangle rectangle, double dx, double dy)
        {
            var _dc = dc as DrawingContext;

            Tuple<Brush, Pen> cache;
            Brush fill;
            Pen stroke;
            if (_enableStyleCache && _styleCache.TryGetValue(rectangle.Style, out cache))
            {
                fill = cache.Item1;
                stroke = cache.Item2;
            }
            else
            {
                fill = CreateBrush(rectangle.Style.Fill);
                stroke = CreatePen(
                    rectangle.Style.Stroke,
                    rectangle.Style.Thickness);

                if (_enableStyleCache)
                    _styleCache.Add(rectangle.Style, Tuple.Create(fill, stroke));
            }

            var rect = CreateRect(
                rectangle.TopLeft,
                rectangle.BottomRight,
                dx, dy);
            _dc.DrawRectangle(
                rectangle.IsFilled ? fill : null,
                stroke,
                rect);
        }

        public void Draw(object dc, XEllipse ellipse, double dx, double dy)
        {
            var _dc = dc as DrawingContext;

            Tuple<Brush, Pen> cache;
            Brush fill;
            Pen stroke;
            if (_enableStyleCache && _styleCache.TryGetValue(ellipse.Style, out cache))
            {
                fill = cache.Item1;
                stroke = cache.Item2;
            }
            else
            {
                fill = CreateBrush(ellipse.Style.Fill);
                stroke = CreatePen(
                    ellipse.Style.Stroke,
                    ellipse.Style.Thickness);

                if (_enableStyleCache)
                    _styleCache.Add(ellipse.Style, Tuple.Create(fill, stroke));
            }

            var rect = CreateRect(
                ellipse.TopLeft,
                ellipse.BottomRight,
                dx, dy);
            double rx = rect.Width / 2.0;
            double ry = rect.Height / 2.0;
            var center = new Point(rect.X + rx, rect.Y + ry);
            _dc.DrawEllipse(
                ellipse.IsFilled ? fill : null,
                stroke,
                center,
                rx, ry);
        }

        public void Draw(object dc, XBezier bezier, double dx, double dy)
        {
            var _dc = dc as DrawingContext;

            Tuple<Brush, Pen> cache;
            Brush fill;
            Pen stroke;
            if (_enableStyleCache && _styleCache.TryGetValue(bezier.Style, out cache))
            {
                fill = cache.Item1;
                stroke = cache.Item2;
            }
            else
            {
                fill = CreateBrush(bezier.Style.Fill);
                stroke = CreatePen(
                    bezier.Style.Stroke,
                    bezier.Style.Thickness);

                if (_enableStyleCache)
                    _styleCache.Add(bezier.Style, Tuple.Create(fill, stroke));
            }

            PathGeometry pg;
            if (_enableBezierCache && _bezierCache.TryGetValue(bezier, out pg))
            {
                var pf = pg.Figures[0];
                pf.StartPoint = new Point(bezier.Point1.X + dx, bezier.Point1.Y + dy);
                pf.IsFilled = bezier.IsFilled;
                var bs = pf.Segments[0] as BezierSegment;
                bs.Point1 = new Point(bezier.Point2.X + dx, bezier.Point2.Y + dy);
                bs.Point2 = new Point(bezier.Point3.X + dx, bezier.Point3.Y + dy);
                bs.Point3 = new Point(bezier.Point4.X + dx, bezier.Point4.Y + dy);
            }
            else
            {
                var pf = new PathFigure()
                {
                    StartPoint = new Point(bezier.Point1.X + dx, bezier.Point1.Y + dy),
                    IsFilled = bezier.IsFilled
                };
                var bs = new BezierSegment(
                        new Point(bezier.Point2.X + dx, bezier.Point2.Y + dy),
                        new Point(bezier.Point3.X + dx, bezier.Point3.Y + dy),
                        new Point(bezier.Point4.X + dx, bezier.Point4.Y + dy),
                        true);
                //bs.Freeze();
                pf.Segments.Add(bs);
                //pf.Freeze();
                pg = new PathGeometry();
                pg.Figures.Add(pf);
                //pg.Freeze();

                if (_enableBezierCache)
                    _bezierCache.Add(bezier, pg);
            }

            _dc.DrawGeometry(bezier.IsFilled ? fill : null, stroke, pg);
        }

        public void Draw(object dc, XQBezier qbezier, double dx, double dy)
        {
            var _dc = dc as DrawingContext;

            Tuple<Brush, Pen> cache;
            Brush fill;
            Pen stroke;
            if (_enableStyleCache && _styleCache.TryGetValue(qbezier.Style, out cache))
            {
                fill = cache.Item1;
                stroke = cache.Item2;
            }
            else
            {
                fill = CreateBrush(qbezier.Style.Fill);
                stroke = CreatePen(
                    qbezier.Style.Stroke,
                    qbezier.Style.Thickness);

                if (_enableStyleCache)
                    _styleCache.Add(qbezier.Style, Tuple.Create(fill, stroke));
            }

            PathGeometry pg;

            if (_enableQBezierCache && _qbezierCache.TryGetValue(qbezier, out pg))
            {
                var pf = pg.Figures[0];
                pf.StartPoint = new Point(qbezier.Point1.X + dx, qbezier.Point1.Y + dy);
                pf.IsFilled = qbezier.IsFilled;
                var qbs = pf.Segments[0] as QuadraticBezierSegment;
                qbs.Point1 = new Point(qbezier.Point2.X + dx, qbezier.Point2.Y + dy);
                qbs.Point2 = new Point(qbezier.Point3.X + dx, qbezier.Point3.Y + dy);
            }
            else
            {
                var pf = new PathFigure()
                {
                    StartPoint = new Point(qbezier.Point1.X + dx, qbezier.Point1.Y + dy),
                    IsFilled = qbezier.IsFilled
                };

                var qbs = new QuadraticBezierSegment(
                        new Point(qbezier.Point2.X + dx, qbezier.Point2.Y + dy),
                        new Point(qbezier.Point3.X + dx, qbezier.Point3.Y + dy),
                        true);
                //bs.Freeze();
                pf.Segments.Add(qbs);
                //pf.Freeze();
                pg = new PathGeometry();
                pg.Figures.Add(pf);
                //pg.Freeze();
                if (_enableQBezierCache)
                    _qbezierCache.Add(qbezier, pg);
            }
            
            _dc.DrawGeometry(qbezier.IsFilled ? fill : null, stroke, pg);
        }
    }
}