// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static System.Math;

namespace Wpf.Controls.PanAndZoom
{
    /// <summary>
    /// 
    /// </summary>
    public class ZoomBorder : Border, IScrollInfo
    {
        private UIElement _element;
        private Point _pan;
        private Point _previous;
        private Matrix _matrix;

        /// <summary>
        /// 
        /// </summary>
        public double ZoomSpeed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public AutoFitMode AutoFitMode { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Action<double, double, double, double> InvalidatedChild { get; set; }


        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Zoom.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register("Zoom", typeof(double), typeof(ZoomBorder), new PropertyMetadata(0.0));



        /// <summary>
        /// 
        /// </summary>
        public ZoomBorder()
            : base()
        {
            _matrix = MatrixHelper.Identity;

            ZoomSpeed = 1.2;
            AutoFitMode = AutoFitMode.None;

            Focusable = true;
            Background = Brushes.Transparent;

            Unloaded += PanAndZoom_Unloaded;
        }

        public override UIElement Child
        {
            set
            {
                base.Child = value;
                Initialize(value);
            }
            get { return _element; }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _element.Arrange(new Rect(new Point(0,0), _element.DesiredSize));
            CheckBounds();
            Invalidate();

            return finalSize;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _element.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            return constraint;
        }

        private void PanAndZoom_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_element != null)
            {
                Unload();
            }
        }    

        private void Initialize(UIElement element)
        {
            if (element != null)
            {
                _element = element;
                this.Focus();
                this.PreviewMouseWheel += Border_PreviewMouseWheel;
                this.PreviewMouseRightButtonDown += Border_PreviewMouseRightButtonDown;
                this.PreviewMouseRightButtonUp += Border_PreviewMouseRightButtonUp;
                this.PreviewMouseMove += Border_PreviewMouseMove;
            }
        }

        private void Unload()
        {
            if (_element != null)
            {
                this.PreviewMouseWheel -= Border_PreviewMouseWheel;
                this.PreviewMouseRightButtonDown -= Border_PreviewMouseRightButtonDown;
                this.PreviewMouseRightButtonUp -= Border_PreviewMouseRightButtonUp;
                this.PreviewMouseMove -= Border_PreviewMouseMove;
                _element.RenderTransform = null;
                _element = null;
            }
        }

        private void Border_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_element != null)
            {
                Point point = e.GetPosition(_element);
                ZoomDeltaTo(e.Delta, point);
            }
        }

        private void Border_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_element != null)
            {
                Point point = e.GetPosition(_element);
                StartPan(point);
                _element.CaptureMouse();
            }
        }

        private void Border_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_element != null)
            {
                _element.ReleaseMouseCapture();
            }
        }

        private void Border_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_element != null && _element.IsMouseCaptured)
            {
                Point point = e.GetPosition(_element);
                PanTo(point);
            }
        }
        

        /// <summary>
        /// 
        /// </summary>
        public void Invalidate()
        {
            if (_element != null)
            {
                this.InvalidatedChild?.Invoke(_matrix.M11, _matrix.M12, _matrix.OffsetX, _matrix.OffsetY);
                _element.RenderTransformOrigin = new Point(0, 0);
                _element.RenderTransform = new MatrixTransform(_matrix);
                _element.InvalidateVisual();
                ScrollOwner?.InvalidateScrollInfo();

                Zoom = _matrix.M11;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="zoom"></param>
        /// <param name="point"></param>
        public void ZoomTo(double zoom, Point point)
        {
            
            _matrix = MatrixHelper.ScaleAtPrepend(_matrix, zoom, zoom, point.X, point.Y);

            CheckBounds();

            Invalidate();
        }

        private void CheckBounds()
        {
            var viewPortSize = this.DesiredSize;

            var minScale = Min(Min(viewPortSize.Height / _element.RenderSize.Height,
                viewPortSize.Width / _element.RenderSize.Width), 1);

            _matrix.M11 = _matrix.M11 < minScale ? minScale : _matrix.M11;
            _matrix.M22 = _matrix.M22 < minScale ? minScale : _matrix.M22;

            var contentSize = new Size(_element.RenderSize.Width * _matrix.M11, _element.RenderSize.Height * _matrix.M22);

            if (contentSize.Width <= viewPortSize.Width)
            {
                _matrix.OffsetX = (viewPortSize.Width - contentSize.Width) / 2;
            }
            else
            {
                _matrix.OffsetX = Max(Min(_matrix.OffsetX, 0), viewPortSize.Width - contentSize.Width);
            }

            if (contentSize.Height <= viewPortSize.Height)
            {
                _matrix.OffsetY = (viewPortSize.Height - contentSize.Height) / 2;
            }
            else
            {
                _matrix.OffsetY = Max(Min(_matrix.OffsetY, 0), viewPortSize.Height - contentSize.Height);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delta"></param>
        /// <param name="point"></param>
        public void ZoomDeltaTo(int delta, Point point)
        {
            ZoomTo(delta > 0 ? ZoomSpeed : 1 / ZoomSpeed, point);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        public void StartPan(Point point)
        {
            _pan = new Point();
            _previous = new Point(point.X, point.Y);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        public void PanTo(Point point)
        {
            var delta = point - _previous;
            _previous = point;

            _pan.Offset(delta.X, delta.Y);
            
            _matrix.TranslatePrepend(_pan.X, _pan.Y);

            CheckBounds();

            Invalidate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="panelSize"></param>
        /// <param name="elementSize"></param>
        public void Extent(Size panelSize, Size elementSize)
        {
            if (_element != null)
            {
                double pw = panelSize.Width;
                double ph = panelSize.Height;
                double ew = elementSize.Width;
                double eh = elementSize.Height;
                double zx = pw / ew;
                double zy = ph / eh;
                double zoom = Min(zx, zy);
                double cx = ew / 2.0;
                double cy = eh / 2.0;

                _matrix = MatrixHelper.ScaleAt(zoom, zoom, cx, cy);

                CheckBounds();
                Invalidate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="panelSize"></param>
        /// <param name="elementSize"></param>
        public void Fill(Size panelSize, Size elementSize)
        {
            if (_element != null)
            {
                double pw = panelSize.Width;
                double ph = panelSize.Height;
                double ew = elementSize.Width;
                double eh = elementSize.Height;
                double zx = pw / ew;
                double zy = ph / eh;
                double scale = Max(zx, zy);

                _matrix = MatrixHelper.ScaleAt(scale, scale, ew / 2.0, eh / 2.0);

                CheckBounds();
                Invalidate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="panelSize"></param>
        /// <param name="elementSize"></param>
        public void AutoFit(Size panelSize, Size elementSize)
        {
            if (_element != null)
            {
                switch (AutoFitMode)
                {
                    case AutoFitMode.None:
                        Reset();
                        break;
                    case AutoFitMode.Extent:
                        Extent(panelSize, elementSize);
                        break;
                    case AutoFitMode.Fill:
                        Fill(panelSize, elementSize);
                        break;
                }

                Invalidate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ToggleAutoFitMode()
        {
            switch (AutoFitMode)
            {
                case AutoFitMode.None:
                    AutoFitMode = AutoFitMode.Extent;
                    break;
                case AutoFitMode.Extent:
                    AutoFitMode = AutoFitMode.Fill;
                    break;
                case AutoFitMode.Fill:
                    AutoFitMode = AutoFitMode.None;
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Reset()
        {
            _matrix = MatrixHelper.Identity;

            CheckBounds();
            Invalidate();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Extent()
        {
            Extent(this.DesiredSize, _element.RenderSize);
            Extent(this.DesiredSize, _element.RenderSize);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Fill()
        {
            Fill(this.DesiredSize, _element.RenderSize);
        }

        /// <summary>
        /// 
        /// </summary>
        public void AutoFit()
        {
            if (_element != null)
            {
                AutoFit(this.DesiredSize, _element.RenderSize);
            }
        }

        public void LineUp()
        {
            throw new NotImplementedException();
        }

        public void LineDown()
        {
            throw new NotImplementedException();
        }

        public void LineLeft()
        {
            throw new NotImplementedException();
        }

        public void LineRight()
        {
            throw new NotImplementedException();
        }

        public void PageUp()
        {
            throw new NotImplementedException();
        }

        public void PageDown()
        {
            throw new NotImplementedException();
        }

        public void PageLeft()
        {
            throw new NotImplementedException();
        }

        public void PageRight()
        {
            throw new NotImplementedException();
        }

        public void MouseWheelUp()
        {
            //if (_element != null)
            //{
            //    Point point = Mouse.GetPosition(_element);
            //    ZoomDeltaTo(1, point);
            //    ScrollOwner.InvalidateScrollInfo();
            //}
        }

        public void MouseWheelDown()
        {
            //if (_element != null)
            //{
            //    Point point = Mouse.GetPosition(_element);
            //    ZoomDeltaTo(-1, point);
            //    ScrollOwner.InvalidateScrollInfo();
            //}
        }

        public void MouseWheelLeft()
        {
            throw new NotImplementedException();
        }

        public void MouseWheelRight()
        {
            throw new NotImplementedException();
        }

        public void SetHorizontalOffset(double offset)
        {
            _matrix.OffsetX = -offset;
            Invalidate();
        }

        public void SetVerticalOffset(double offset)
        {
            _matrix.OffsetY = -offset;
            Invalidate();
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            return new Rect(new Point(0,0), DesiredSize);
        }

        public bool CanVerticallyScroll { get; set; }
        public bool CanHorizontallyScroll { get; set; }
        public double ExtentWidth => _element.DesiredSize.Width*_matrix.M11;
        public double ExtentHeight => _element.DesiredSize.Height * _matrix.M22;
        public double ViewportWidth => ActualWidth;
        public double ViewportHeight => ActualHeight;
        public double HorizontalOffset => -_matrix.OffsetX;
        public double VerticalOffset => -_matrix.OffsetY;
        public ScrollViewer ScrollOwner { get; set; }
    }
}
