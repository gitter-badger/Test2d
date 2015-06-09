﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test2d;

namespace TestEDITOR
{
    /// <summary>
    /// 
    /// </summary>
    public class EmfRenderer : ObservableObject, IRenderer
    {
        private bool _enableImageCache = true;
        private IDictionary<Uri, Image> _biCache;
        private RendererState _state = new RendererState();

        /// <summary>
        /// 
        /// </summary>
        public RendererState State
        {
            get { return _state; }
            set { Update(ref _state, value); }
        }
        
        /// <summary>
        /// 
        /// </summary>
        private Func<double, float> _scaleToPage;

        /// <summary>
        /// 
        /// </summary>
        private double _textScaleFactor;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="textScaleFactor"></param>
        public EmfRenderer(double textScaleFactor = 1.0)
        {
            ClearCache();

            _textScaleFactor = textScaleFactor;
            _scaleToPage = (value) => (float)(value);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static IRenderer Create()
        {
            return new EmfRenderer();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private Color ToColor(ArgbColor color)
        {
            return Color.FromArgb(
                color.A,
                color.R,
                color.G,
                color.B);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="style"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private Pen ToPen(BaseStyle style, Func<double, float> scale)
        {
            var pen = new Pen(ToColor(style.Stroke), (float)(style.Thickness / _state.Zoom));
            switch (style.LineCap)
            {
                case Test2d.LineCap.Flat:
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Flat;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Flat;
                    pen.DashCap = System.Drawing.Drawing2D.DashCap.Flat;
                    break;
                case Test2d.LineCap.Square:
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Square;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Square;
                    pen.DashCap = System.Drawing.Drawing2D.DashCap.Flat;
                    break;
                case Test2d.LineCap.Round:
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.DashCap = System.Drawing.Drawing2D.DashCap.Round;
                    break;
            }
            if (style.Dashes != null)
            {
                // TODO: Convert to correct dash values.
                pen.DashPattern = style.Dashes.Select(x => (float)x).ToArray();
            }
            pen.DashOffset = (float)style.DashOffset;
            return pen;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private SolidBrush ToSolidBrush(ArgbColor color)
        {
            return new SolidBrush(ToColor(color));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tl"></param>
        /// <param name="br"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns></returns>
        private static Rect2 CreateRect(XPoint tl, XPoint br, double dx, double dy)
        {
            return Rect2.Create(tl, br, dx, dy);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="pen"></param>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        private static void DrawLineInternal(
            Graphics gfx,
            Pen pen,
            ref PointF p0,
            ref PointF p1)
        {
            gfx.DrawLine(pen, p0, p1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="brush"></param>
        /// <param name="pen"></param>
        /// <param name="isFilled"></param>
        /// <param name="rect"></param>
        private static void DrawRectangleInternal(
            Graphics gfx,
            Brush brush,
            Pen pen,
            bool isFilled,
            ref Rect2 rect)
        {
            if (isFilled)
            {
                gfx.FillRectangle(
                    brush,
                    (float)rect.X,
                    (float)rect.Y,
                    (float)rect.Width,
                    (float)rect.Height);
            }

            gfx.DrawRectangle(
                pen,
                (float)rect.X,
                (float)rect.Y,
                (float)rect.Width,
                (float)rect.Height);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="brush"></param>
        /// <param name="pen"></param>
        /// <param name="isFilled"></param>
        /// <param name="rect"></param>
        private static void DrawEllipseInternal(
            Graphics gfx,
            Brush brush,
            Pen pen,
            bool isFilled,
            ref Rect2 rect)
        {
            if (isFilled)
            {
                gfx.FillEllipse(
                    brush,
                    (float)rect.X,
                    (float)rect.Y,
                    (float)rect.Width,
                    (float)rect.Height);
            }

            gfx.DrawEllipse(
                pen,
                (float)rect.X,
                (float)rect.Y,
                (float)rect.Width,
                (float)rect.Height);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="container"></param>
        private void DrawBackgroundInternal(Graphics gfx, Container container)
        {
            Brush brush = ToSolidBrush(container.Background);
            var rect = Rect2.Create(0, 0, container.Width, container.Height);
            gfx.FillRectangle(
                brush,
                _scaleToPage(rect.X),
                _scaleToPage(rect.Y),
                _scaleToPage(rect.Width),
                _scaleToPage(rect.Height));
            brush.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearCache()
        {
            if (_biCache != null)
            {
                foreach (var kvp in _biCache)
                {
                    kvp.Value.Dispose();
                }
                _biCache.Clear();
            }
            _biCache = new Dictionary<Uri, Image>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="container"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, Container container, ImmutableArray<ShapeProperty> db, Record r)
        {
            foreach (var layer in container.Layers)
            {
                if (layer.IsVisible)
                {
                    Draw(gfx, layer, db, r);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="layer"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, Layer layer, ImmutableArray<ShapeProperty> db, Record r)
        {
            foreach (var shape in layer.Shapes)
            {
                if (shape.State.HasFlag(_state.DrawShapeState))
                {
                    shape.Draw(gfx, this, 0, 0, db, r);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="line"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XLine line, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;

            Brush fillLine = ToSolidBrush(line.Style.Fill);
            Pen strokeLine = ToPen(line.Style, _scaleToPage);

            Brush fillStartArrow = ToSolidBrush(line.Style.StartArrowStyle.Fill);
            Pen strokeStartArrow = ToPen(line.Style.StartArrowStyle, _scaleToPage);

            Brush fillEndArrow = ToSolidBrush(line.Style.EndArrowStyle.Fill);
            Pen strokeEndArrow = ToPen(line.Style.EndArrowStyle, _scaleToPage);

            double _x1 = line.Start.X + dx;
            double _y1 = line.Start.Y + dy;
            double _x2 = line.End.X + dx;
            double _y2 = line.End.Y + dy;

            XLine.SetMaxLength(line, ref _x1, ref _y1, ref _x2, ref _y2);

            float x1 = _scaleToPage(_x1);
            float y1 = _scaleToPage(_y1);
            float x2 = _scaleToPage(_x2);
            float y2 = _scaleToPage(_y2);

            var sas = line.Style.StartArrowStyle;
            var eas = line.Style.EndArrowStyle;
            float a1 = (float)(Math.Atan2(y1 - y2, x1 - x2) * 180.0 / Math.PI);
            float a2 = (float)(Math.Atan2(y2 - y1, x2 - x1) * 180.0 / Math.PI);

            var t1 = new Matrix();
            var c1 = new PointF(x1, y1);
            t1.RotateAt(a1, c1);

            var t2 = new Matrix();
            var c2 = new PointF(x2, y2);
            t2.RotateAt(a2, c2);

            PointF pt1;
            PointF pt2;

            double radiusX1 = sas.RadiusX;
            double radiusY1 = sas.RadiusY;
            double sizeX1 = 2.0 * radiusX1;
            double sizeY1 = 2.0 * radiusY1;

            switch (sas.ArrowType)
            {
                default:
                case ArrowType.None:
                    {
                        pt1 = new PointF(x1, y1);
                    }
                    break;
                case ArrowType.Rectangle:
                    {
                        var pts = new PointF[] { new PointF(x1 - (float)sizeX1, y1) };
                        t1.TransformPoints(pts);
                        pt1 = pts[0];
                        var rect = new Rect2(x1 - sizeX1, y1 - radiusY1, sizeX1, sizeY1);
                        var gs = _gfx.Save();
                        _gfx.MultiplyTransform(t1);
                        DrawRectangleInternal(_gfx, fillStartArrow, strokeStartArrow, sas.IsFilled, ref rect);
                        _gfx.Restore(gs);
                    }
                    break;
                case ArrowType.Ellipse:
                    {
                        var pts = new PointF[] { new PointF(x1 - (float)sizeX1, y1) };
                        t1.TransformPoints(pts);
                        pt1 = pts[0];
                        var gs = _gfx.Save();
                        _gfx.MultiplyTransform(t1);
                        var rect = new Rect2(x1 - sizeX1, y1 - radiusY1, sizeX1, sizeY1);
                        DrawEllipseInternal(_gfx, fillStartArrow, strokeStartArrow, sas.IsFilled, ref rect);
                        _gfx.Restore(gs);
                    }
                    break;
                case ArrowType.Arrow:
                    {
                        var pts = new PointF[] 
                        { 
                            new PointF(x1, y1),
                            new PointF(x1 - (float)sizeX1, y1 + (float)sizeY1),
                            new PointF(x1, y1),
                            new PointF(x1 - (float)sizeX1, y1 - (float)sizeY1),
                            new PointF(x1, y1)
                        };
                        t1.TransformPoints(pts);
                        pt1 = pts[0];
                        var p11 = pts[1];
                        var p21 = pts[2];
                        var p12 = pts[3];
                        var p22 = pts[4];
                        DrawLineInternal(_gfx, strokeStartArrow, ref p11, ref p21);
                        DrawLineInternal(_gfx, strokeStartArrow, ref p12, ref p22);
                    }
                    break;
            }

            double radiusX2 = eas.RadiusX;
            double radiusY2 = eas.RadiusY;
            double sizeX2 = 2.0 * radiusX2;
            double sizeY2 = 2.0 * radiusY2;

            switch (eas.ArrowType)
            {
                default:
                case ArrowType.None:
                    {
                        pt2 = new PointF(x2, y2);
                    }
                    break;
                case ArrowType.Rectangle:
                    {
                        var pts = new PointF[] { new PointF(x2 - (float)sizeX2, y2) };
                        t2.TransformPoints(pts);
                        pt2 = pts[0];
                        var rect = new Rect2(x2 - sizeX2, y2 - radiusY2, sizeX2, sizeY2);
                        var gs = _gfx.Save();
                        _gfx.MultiplyTransform(t2);
                        DrawRectangleInternal(_gfx, fillEndArrow, strokeEndArrow, eas.IsFilled, ref rect);
                        _gfx.Restore(gs);
                    }
                    break;
                case ArrowType.Ellipse:
                    {
                        var pts = new PointF[] { new PointF(x2 - (float)sizeX2, y2) };
                        t2.TransformPoints(pts);
                        pt2 = pts[0];
                        var gs = _gfx.Save();
                        _gfx.MultiplyTransform(t2);
                        var rect = new Rect2(x2 - sizeX2, y2 - radiusY2, sizeX2, sizeY2);
                        DrawEllipseInternal(_gfx, fillEndArrow, strokeEndArrow, eas.IsFilled, ref rect);
                        _gfx.Restore(gs);
                    }
                    break;
                case ArrowType.Arrow:
                    {
                        var pts = new PointF[] 
                        { 
                            new PointF(x2, y2),
                            new PointF(x2 - (float)sizeX2, y2 + (float)sizeY2),
                            new PointF(x2, y2),
                            new PointF(x2 - (float)sizeX2, y2 - (float)sizeY2),
                            new PointF(x2, y2)
                        };
                        t2.TransformPoints(pts);
                        pt2 = pts[0];
                        var p11 = pts[1];
                        var p21 = pts[2];
                        var p12 = pts[3];
                        var p22 = pts[4];
                        DrawLineInternal(_gfx, strokeEndArrow, ref p11, ref p21);
                        DrawLineInternal(_gfx, strokeEndArrow, ref p12, ref p22);
                    }
                    break;
            }

            _gfx.DrawLine(strokeLine, pt1, pt2);

            fillLine.Dispose();
            strokeLine.Dispose();

            fillStartArrow.Dispose();
            strokeStartArrow.Dispose();

            fillEndArrow.Dispose();
            strokeEndArrow.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="rectangle"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XRectangle rectangle, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;

            Brush brush = ToSolidBrush(rectangle.Style.Fill);
            Pen pen = ToPen(rectangle.Style, _scaleToPage);

            var rect = CreateRect(
                rectangle.TopLeft,
                rectangle.BottomRight,
                dx, dy);

            if (rectangle.IsFilled)
            {
                _gfx.FillRectangle(
                    brush,
                    _scaleToPage(rect.X),
                    _scaleToPage(rect.Y),
                    _scaleToPage(rect.Width),
                    _scaleToPage(rect.Height));
            }

            _gfx.DrawRectangle(
                pen,
                _scaleToPage(rect.X),
                _scaleToPage(rect.Y),
                _scaleToPage(rect.Width),
                _scaleToPage(rect.Height));

            brush.Dispose();
            pen.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="ellipse"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XEllipse ellipse, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;

            Brush brush = ToSolidBrush(ellipse.Style.Fill);
            Pen pen = ToPen(ellipse.Style, _scaleToPage);

            var rect = CreateRect(
                ellipse.TopLeft,
                ellipse.BottomRight,
                dx, dy);

            if (ellipse.IsFilled)
            {
                _gfx.FillEllipse(
                    brush,
                    _scaleToPage(rect.X),
                    _scaleToPage(rect.Y),
                    _scaleToPage(rect.Width),
                    _scaleToPage(rect.Height));
            }

            _gfx.DrawEllipse(
                pen,
                _scaleToPage(rect.X),
                _scaleToPage(rect.Y),
                _scaleToPage(rect.Width),
                _scaleToPage(rect.Height));

            brush.Dispose();
            pen.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="arc"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XArc arc, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var a = GdiArc.FromXArc(arc, dx, dy);
            if (a.Width <= 0.0 || a.Height <= 0.0)
                return;

            var _gfx = gfx as Graphics;

            Brush brush = ToSolidBrush(arc.Style.Fill);
            Pen pen = ToPen(arc.Style, _scaleToPage);

            if (arc.IsFilled)
            {
                var path = new GraphicsPath();
                path.AddArc(
                    _scaleToPage(a.X),
                    _scaleToPage(a.Y),
                    _scaleToPage(a.Width),
                    _scaleToPage(a.Height),
                    (float)a.StartAngle,
                    (float)a.SweepAngle);
                _gfx.FillPath(brush, path);
            }

            _gfx.DrawArc(
                pen,
                _scaleToPage(a.X),
                _scaleToPage(a.Y),
                _scaleToPage(a.Width),
                _scaleToPage(a.Height),
                (float)a.StartAngle,
                (float)a.SweepAngle);

            brush.Dispose();
            pen.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="bezier"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XBezier bezier, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;

            Brush brush = ToSolidBrush(bezier.Style.Fill);
            Pen pen = ToPen(bezier.Style, _scaleToPage);

            if (bezier.IsFilled)
            {
                var path = new GraphicsPath();
                path.AddBezier(
                    _scaleToPage(bezier.Point1.X),
                    _scaleToPage(bezier.Point1.Y),
                    _scaleToPage(bezier.Point2.X), 
                    _scaleToPage(bezier.Point2.Y),
                    _scaleToPage(bezier.Point3.X), 
                    _scaleToPage(bezier.Point3.Y),
                    _scaleToPage(bezier.Point4.X),
                    _scaleToPage(bezier.Point4.Y));
                _gfx.FillPath(brush, path);
            }

            _gfx.DrawBezier(
                pen,
                _scaleToPage(bezier.Point1.X),
                _scaleToPage(bezier.Point1.Y),
                _scaleToPage(bezier.Point2.X),
                _scaleToPage(bezier.Point2.Y),
                _scaleToPage(bezier.Point3.X),
                _scaleToPage(bezier.Point3.Y),
                _scaleToPage(bezier.Point4.X),
                _scaleToPage(bezier.Point4.Y));

            brush.Dispose();
            pen.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="qbezier"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XQBezier qbezier, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;

            Brush brush = ToSolidBrush(qbezier.Style.Fill);
            Pen pen = ToPen(qbezier.Style, _scaleToPage);

            double x1 = qbezier.Point1.X;
            double y1 = qbezier.Point1.Y;
            double x2 = qbezier.Point1.X + (2.0 * (qbezier.Point2.X - qbezier.Point1.X)) / 3.0;
            double y2 = qbezier.Point1.Y + (2.0 * (qbezier.Point2.Y - qbezier.Point1.Y)) / 3.0;
            double x3 = x2 + (qbezier.Point3.X - qbezier.Point1.X) / 3.0;
            double y3 = y2 + (qbezier.Point3.Y - qbezier.Point1.Y) / 3.0;
            double x4 = qbezier.Point3.X;
            double y4 = qbezier.Point3.Y;

            if (qbezier.IsFilled)
            {
                var path = new GraphicsPath();
                path.AddBezier(
                    _scaleToPage(x1 + dx), 
                    _scaleToPage(y1 + dy),
                    _scaleToPage(x2 + dx), 
                    _scaleToPage(y2 + dy),
                    _scaleToPage(x3 + dx), 
                    _scaleToPage(y3 + dy),
                    _scaleToPage(x4 + dx), 
                    _scaleToPage(y4 + dy));
                _gfx.FillPath(brush, path);
            }
            
            _gfx.DrawBezier(
                pen,
                _scaleToPage(x1 + dx), 
                _scaleToPage(y1 + dy),
                _scaleToPage(x2 + dx), 
                _scaleToPage(y2 + dy),
                _scaleToPage(x3 + dx), 
                _scaleToPage(y3 + dy),
                _scaleToPage(x4 + dx), 
                _scaleToPage(y4 + dy));

            brush.Dispose();
            pen.Dispose();
        }
   
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="text"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XText text, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;

            Brush brush = ToSolidBrush(text.Style.Stroke);

            var fontStyle = System.Drawing.FontStyle.Regular;
            if (text.Style.TextStyle.FontStyle.HasFlag(Test2d.FontStyle.Bold))
            {
                fontStyle |= System.Drawing.FontStyle.Bold;
            }

            if (text.Style.TextStyle.FontStyle.HasFlag(Test2d.FontStyle.Italic))
            {
                fontStyle |= System.Drawing.FontStyle.Italic;
            }

            if (text.Style.TextStyle.FontStyle.HasFlag(Test2d.FontStyle.Underline))
            {
                fontStyle |= System.Drawing.FontStyle.Underline;
            }

            if (text.Style.TextStyle.FontStyle.HasFlag(Test2d.FontStyle.Strikeout))
            {
                fontStyle |= System.Drawing.FontStyle.Strikeout;
            }

            Font font = new Font(
                text.Style.TextStyle.FontName, 
                (float)(text.Style.TextStyle.FontSize * _textScaleFactor),
                fontStyle);

            var rect = CreateRect(
                text.TopLeft,
                text.BottomRight,
                dx, dy);

            var srect = new RectangleF(
                _scaleToPage(rect.X),
                _scaleToPage(rect.Y),
                _scaleToPage(rect.Width),
                _scaleToPage(rect.Height));

            var format = new StringFormat();
            switch (text.Style.TextStyle.TextHAlignment)
            {
                case TextHAlignment.Left:
                    format.Alignment = StringAlignment.Near;
                    break;
                case TextHAlignment.Center:
                    format.Alignment = StringAlignment.Center;
                    break;
                case TextHAlignment.Right:
                    format.Alignment = StringAlignment.Far;
                    break;
            }

            switch (text.Style.TextStyle.TextVAlignment)
            {
                case TextVAlignment.Top:
                    format.LineAlignment = StringAlignment.Near;
                    break;
                case TextVAlignment.Center:
                    format.LineAlignment = StringAlignment.Center;
                    break;
                case TextVAlignment.Bottom:
                    format.LineAlignment = StringAlignment.Far;
                    break;
            }

            if (text.IsFilled)
            {
                _gfx.FillRectangle(ToSolidBrush(text.Style.Fill), srect);
            }

            _gfx.DrawString(
                text.BindToTextProperty(db, r),
                font,
                ToSolidBrush(text.Style.Stroke),
                srect,
                format);

            brush.Dispose();
            font.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="image"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XImage image, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;

            Brush brush = ToSolidBrush(image.Style.Stroke);

            var rect = CreateRect(
                image.TopLeft,
                image.BottomRight,
                dx, dy);

            var srect = new RectangleF(
                _scaleToPage(rect.X),
                _scaleToPage(rect.Y),
                _scaleToPage(rect.Width),
                _scaleToPage(rect.Height));

            if (image.IsFilled)
            {
                _gfx.FillRectangle(ToSolidBrush(image.Style.Fill), srect);
            }

            if (_enableImageCache
                && _biCache.ContainsKey(image.Path))
            {
                _gfx.DrawImage(_biCache[image.Path], srect);
            }
            else
            {
                if (!image.Path.IsAbsoluteUri || !System.IO.File.Exists(image.Path.LocalPath))
                    return;

                var bi = Image.FromFile(image.Path.LocalPath);

                if (_enableImageCache)
                    _biCache[image.Path] = bi;

                _gfx.DrawImage(bi, srect);

                if (!_enableImageCache)
                    bi.Dispose();
            }

            brush.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gfx"></param>
        /// <param name="path"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="db"></param>
        /// <param name="r"></param>
        public void Draw(object gfx, XPath path, double dx, double dy, ImmutableArray<ShapeProperty> db, Record r)
        {
            var _gfx = gfx as Graphics;
            var pg = path.Geometry;
            var gp = new GraphicsPath();
            gp.FillMode = pg.FillRule == Test2d.XFillRule.EvenOdd ? FillMode.Winding : FillMode.Alternate;

            foreach (var pf in pg.Figures)
            {
                var startPoint = pf.StartPoint;

                foreach (var segment in pf.Segments)
                {
                    if (segment is Test2d.XArcSegment)
                    {
                        throw new NotSupportedException("Not supported segment type: " + segment.GetType());
                        //var arcSegment = segment as Test2d.XArcSegment;
                        // TODO: Convert WPF/SVG elliptical arc segment format to GDI+ bezier curves.
                        //startPoint = arcSegment.Point;
                    }
                    else if (segment is Test2d.XBezierSegment)
                    {
                        var bezierSegment = segment as Test2d.XBezierSegment;
                        gp.AddBezier(
                            _scaleToPage(startPoint.X),
                            _scaleToPage(startPoint.Y),
                            _scaleToPage(bezierSegment.Point1.X),
                            _scaleToPage(bezierSegment.Point1.Y),
                            _scaleToPage(bezierSegment.Point2.X),
                            _scaleToPage(bezierSegment.Point2.Y),
                            _scaleToPage(bezierSegment.Point3.X),
                            _scaleToPage(bezierSegment.Point3.Y));
                        startPoint = bezierSegment.Point3;
                    }
                    else if (segment is Test2d.XLineSegment)
                    {
                        var lineSegment = segment as Test2d.XLineSegment;
                        gp.AddLine(
                            _scaleToPage(startPoint.X),
                            _scaleToPage(startPoint.Y),
                            _scaleToPage(lineSegment.Point.X),
                            _scaleToPage(lineSegment.Point.Y));
                        startPoint = lineSegment.Point;
                    }
                    else if (segment is Test2d.XPolyBezierSegment)
                    {
                        var polyBezierSegment = segment as Test2d.XPolyBezierSegment;
                        if (polyBezierSegment.Points.Count >= 3)
                        {
                            gp.AddBezier(
                                _scaleToPage(startPoint.X),
                                _scaleToPage(startPoint.Y),
                                _scaleToPage(polyBezierSegment.Points[0].X),
                                _scaleToPage(polyBezierSegment.Points[0].Y),
                                _scaleToPage(polyBezierSegment.Points[1].X),
                                _scaleToPage(polyBezierSegment.Points[1].Y),
                                _scaleToPage(polyBezierSegment.Points[2].X),
                                _scaleToPage(polyBezierSegment.Points[2].Y));
                        }

                        if (polyBezierSegment.Points.Count > 3 
                            && polyBezierSegment.Points.Count % 3 == 0)
                        {
                            for (int i = 3; i < polyBezierSegment.Points.Count; i += 3)
                            {
                                gp.AddBezier(
                                    _scaleToPage(polyBezierSegment.Points[i - 1].X),
                                    _scaleToPage(polyBezierSegment.Points[i - 1].Y),
                                    _scaleToPage(polyBezierSegment.Points[i].X),
                                    _scaleToPage(polyBezierSegment.Points[i].Y),
                                    _scaleToPage(polyBezierSegment.Points[i + 1].X),
                                    _scaleToPage(polyBezierSegment.Points[i + 1].Y),
                                    _scaleToPage(polyBezierSegment.Points[i + 2].X),
                                    _scaleToPage(polyBezierSegment.Points[i + 2].Y));
                            }
                        }

                        startPoint = polyBezierSegment.Points.Last();
                    }
                    else if (segment is Test2d.XPolyLineSegment)
                    {
                        var polyLineSegment = segment as Test2d.XPolyLineSegment;
                        if (polyLineSegment.Points.Count >= 1)
                        {
                            gp.AddLine(
                                _scaleToPage(startPoint.X),
                                _scaleToPage(startPoint.Y),
                                _scaleToPage(polyLineSegment.Points[0].X),
                                _scaleToPage(polyLineSegment.Points[0].Y));
                        }

                        if (polyLineSegment.Points.Count > 1)
                        {
                            for (int i = 1; i < polyLineSegment.Points.Count; i++)
                            {
                                gp.AddLine(
                                    _scaleToPage(polyLineSegment.Points[i - 1].X),
                                    _scaleToPage(polyLineSegment.Points[i - 1].Y),
                                    _scaleToPage(polyLineSegment.Points[i].X),
                                    _scaleToPage(polyLineSegment.Points[i].Y));
                            }
                        }

                        startPoint = polyLineSegment.Points.Last();
                    }
                    else if (segment is Test2d.XPolyQuadraticBezierSegment)
                    {
                        var polyQuadraticSegment = segment as Test2d.XPolyQuadraticBezierSegment;
                        if (polyQuadraticSegment.Points.Count >= 2)
                        {
                            var p1 = startPoint;
                            var p2 = polyQuadraticSegment.Points[0];
                            var p3 = polyQuadraticSegment.Points[1];
                            double x1 = p1.X;
                            double y1 = p1.Y;
                            double x2 = p1.X + (2.0 * (p2.X - p1.X)) / 3.0;
                            double y2 = p1.Y + (2.0 * (p2.Y - p1.Y)) / 3.0;
                            double x3 = x2 + (p3.X - p1.X) / 3.0;
                            double y3 = y2 + (p3.Y - p1.Y) / 3.0;
                            double x4 = p3.X;
                            double y4 = p3.Y;
                            gp.AddBezier(
                                _scaleToPage(x1 + dx),
                                _scaleToPage(y1 + dy),
                                _scaleToPage(x2 + dx),
                                _scaleToPage(y2 + dy),
                                _scaleToPage(x3 + dx),
                                _scaleToPage(y3 + dy),
                                _scaleToPage(x4 + dx),
                                _scaleToPage(y4 + dy));
                        }

                        if (polyQuadraticSegment.Points.Count > 2
                            && polyQuadraticSegment.Points.Count % 2 == 0)
                        {
                            for (int i = 3; i < polyQuadraticSegment.Points.Count; i += 3)
                            {
                                var p1 = polyQuadraticSegment.Points[i - 1];
                                var p2 = polyQuadraticSegment.Points[i];
                                var p3 = polyQuadraticSegment.Points[i + 1];
                                double x1 = p1.X;
                                double y1 = p1.Y;
                                double x2 = p1.X + (2.0 * (p2.X - p1.X)) / 3.0;
                                double y2 = p1.Y + (2.0 * (p2.Y - p1.Y)) / 3.0;
                                double x3 = x2 + (p3.X - p1.X) / 3.0;
                                double y3 = y2 + (p3.Y - p1.Y) / 3.0;
                                double x4 = p3.X;
                                double y4 = p3.Y;
                                gp.AddBezier(
                                    _scaleToPage(x1 + dx),
                                    _scaleToPage(y1 + dy),
                                    _scaleToPage(x2 + dx),
                                    _scaleToPage(y2 + dy),
                                    _scaleToPage(x3 + dx),
                                    _scaleToPage(y3 + dy),
                                    _scaleToPage(x4 + dx),
                                    _scaleToPage(y4 + dy));
                            }
                        }

                        startPoint = polyQuadraticSegment.Points.Last();
                    }
                    else if (segment is Test2d.XQuadraticBezierSegment)
                    {
                        var qbezierSegment = segment as Test2d.XQuadraticBezierSegment;
                        var p1 = startPoint;
                        var p2 = qbezierSegment.Point1;
                        var p3 = qbezierSegment.Point2;
                        double x1 = p1.X;
                        double y1 = p1.Y;
                        double x2 = p1.X + (2.0 * (p2.X - p1.X)) / 3.0;
                        double y2 = p1.Y + (2.0 * (p2.Y - p1.Y)) / 3.0;
                        double x3 = x2 + (p3.X - p1.X) / 3.0;
                        double y3 = y2 + (p3.Y - p1.Y) / 3.0;
                        double x4 = p3.X;
                        double y4 = p3.Y;
                        gp.AddBezier(
                            _scaleToPage(x1 + dx),
                            _scaleToPage(y1 + dy),
                            _scaleToPage(x2 + dx), 
                            _scaleToPage(y2 + dy),
                            _scaleToPage(x3 + dx), 
                            _scaleToPage(y3 + dy),
                            _scaleToPage(x4 + dx),
                            _scaleToPage(y4 + dy));
                        startPoint = qbezierSegment.Point2;
                    }
                    else
                    {
                        throw new NotSupportedException("Not supported segment type: " + segment.GetType());
                    }
                }

                if (pf.IsClosed)
                {
                    gp.CloseFigure();
                }
                else
                {
                    gp.StartFigure();
                }
            }

            var t = path.Transform;
            var m = new Matrix();
            var c = new PointF(
                _scaleToPage(t.CenterX + dx), 
                _scaleToPage(t.CenterY + dy));

            // translate
            m.Translate(
                _scaleToPage(t.OffsetX + dx),
                _scaleToPage(t.OffsetY + dy));
            // rotate
            m.RotateAt((float)t.RotateAngle, c);
            // skew
            m.Translate(-c.X, -c.Y);
            m.Multiply(
                new Matrix(
                    1, 
                    (float)Math.Tan(Math.PI * t.SkewAngleY / 180.0), 
                    (float)Math.Tan(Math.PI * t.SkewAngleX / 180.0), 
                    1, 
                    0, 
                    0),
                MatrixOrder.Prepend);
            m.Translate(c.X, c.Y);
            // scale
             m.Multiply(
                new Matrix(
                    (float)t.ScaleX, 
                    0, 
                    0, 
                    (float)t.ScaleY, 
                    (float)(c.X - c.X * t.ScaleX),
                    (float)(c.Y - c.Y * t.ScaleY)),
                MatrixOrder.Prepend);
 
            var gs = _gfx.Save();
            _gfx.Transform = m;

            if (path.IsFilled && path.IsStroked)
            {
                var brush = ToSolidBrush(path.Style.Fill);
                var pen = ToPen(path.Style, _scaleToPage);
                _gfx.FillPath(
                    brush,
                    gp);
                _gfx.DrawPath(
                    pen,
                    gp);
                brush.Dispose();
                pen.Dispose();
            }
            else if (path.IsFilled && !path.IsStroked)
            {
                var brush = ToSolidBrush(path.Style.Fill);
                _gfx.FillPath(
                    brush,
                    gp);
                brush.Dispose();
            }
            else if (!path.IsFilled && path.IsStroked)
            {
                var pen = ToPen(path.Style, _scaleToPage);
                _gfx.DrawPath(
                    pen,
                    gp);
                pen.Dispose();
            }

            _gfx.Restore(gs);
        }
    }
}