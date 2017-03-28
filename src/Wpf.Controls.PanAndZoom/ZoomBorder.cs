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
    [TemplatePart(Name = "PART_Content", Type = typeof(ContentPresenter))]
    public class ZoomBorder : ContentControl, IScrollInfo
    {
        private UIElement _element;
        private Point _pan;
        private Point _previous;
        private Matrix _matrix;
            
        /// <summary>
        /// 
        /// </summary>
        public double ZoomSpeed
        {
            get { return (double)GetValue(ZoomSpeedProperty); }
            set { SetValue(ZoomSpeedProperty, value); }
        }
        
        public static readonly DependencyProperty ZoomSpeedProperty =
            DependencyProperty.Register("ZoomSpeed", typeof(double), typeof(ZoomBorder), new PropertyMetadata(1.2));


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
        
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register("Zoom", typeof(double), typeof(ZoomBorder), new PropertyMetadata(0.0));




        public double MinZoom
        {
            get { return (double)GetValue(MinZoomProperty); }
            set { SetValue(MinZoomProperty, value); }
        }
        
        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register("MinZoom", typeof(double), typeof(ZoomBorder), new PropertyMetadata(1.0));





        public double MaxZoom
        {
            get { return (double)GetValue(MaxZoomProperty); }
            set { SetValue(MaxZoomProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MaxZoom.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register("MaxZoom", typeof(double), typeof(ZoomBorder), new PropertyMetadata(10.0));

        static ZoomBorder()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZoomBorder),
                new FrameworkPropertyMetadata(typeof(ZoomBorder)));
        }

        /// <summary>
        /// 
        /// </summary>
        public ZoomBorder()
            : base()
        {
            _matrix = Matrix.Identity;

            ZoomSpeed = 1.2;
            AutoFitMode = AutoFitMode.None;

            Focusable = true;

            Unloaded += PanAndZoom_Unloaded;
        }

        //public override UIElement Child
        //{
        //    set
        //    {
        //        base.Child = value;
        //        Initialize(value);
        //    }
        //    get { return _element; }
        //}

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var element = GetTemplateChild("PART_Content") as ContentPresenter;
            Initialize(element);
        }

        //protected override Size MeasureOverride(Size constraint)
        //{
        //    _element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        //    if (double.IsPositiveInfinity(constraint.Height) || double.IsPositiveInfinity(constraint.Width))
        //    {
        //        return _element.DesiredSize;
        //    }

        //    return constraint;
        //}

        protected override Size ArrangeOverride(Size finalSize)
        {
            //_element.Arrange(new Rect(new Point(0, 0), _element.DesiredSize));
            base.ArrangeOverride(finalSize);

            AutoFit();

            CheckBounds();
            Invalidate();        

            return finalSize;
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
                this.ManipulationDelta += Border_ManipulationDelta;
            }
        }

        private void Border_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            _matrix.Scale(e.DeltaManipulation.Scale.X, e.DeltaManipulation.Scale.Y);
            CheckBounds();
            Invalidate();
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
                _element.RenderTransform = new MatrixTransform(_matrix);
                ((UIElement)GetVisualChild(0)).InvalidateVisual();
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
            if (_matrix.M11*zoom > MaxZoom)
            {
                zoom = MaxZoom / _matrix.M11;
            }

            _matrix.ScaleAtPrepend(zoom, zoom, point.X, point.Y);

            CheckBounds();  

            Invalidate();
        }

        private void CheckBounds()
        {
            var viewPortSize = this.DesiredSize;

            var minScale = Min(Min(viewPortSize.Height / _element.RenderSize.Height,
                viewPortSize.Width / _element.RenderSize.Width), MinZoom);

            _matrix.M11 = _matrix.M11 < minScale ? minScale : _matrix.M11;
            _matrix.M22 = _matrix.M22 < minScale ? minScale : _matrix.M22;

            var contentSize = new Size(_element.DesiredSize.Width * _matrix.M11, _element.DesiredSize.Height * _matrix.M22);

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
            AutoFitMode = AutoFitMode.None;
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
            AutoFitMode = AutoFitMode.None;
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

                _matrix = Matrix.Identity;
                _matrix.ScaleAt(zoom, zoom, cx, cy);

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

                _matrix = Matrix.Identity;
                _matrix.Translate((pw - ew) / 2, (ph - eh) / 2);
                _matrix.ScaleAt(scale, scale, pw / 2, ph / 2);


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
                        break;
                    case AutoFitMode.Extent:
                        Extent(panelSize, elementSize);
                        break;
                    case AutoFitMode.Fill:
                        Fill(panelSize, elementSize);
                        break;
                }               
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
            _matrix = Matrix.Identity;
            CheckBounds();
            Invalidate();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Extent()
        {
            AutoFitMode = AutoFitMode.Extent;
            Extent(this.DesiredSize, _element.RenderSize);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Fill()
        {
            AutoFitMode = AutoFitMode.Fill;
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

        #region IScrollInfo

        public void LineUp()
        {
            PageUp();
        }

        public void LineDown()
        {
            PageDown();
        }

        public void LineLeft()
        {
            PageLeft();
        }

        public void LineRight()
        {
            PageRight();
        }

        public void PageUp()
        {
            _matrix.TranslatePrepend(0, 10);
            CheckBounds();
            Invalidate();
        }

        public void PageDown()
        {
            _matrix.TranslatePrepend(0, -10);
            CheckBounds();
            Invalidate();
        }

        public void PageLeft()
        {
            _matrix.TranslatePrepend(10, 0);
            CheckBounds();
            Invalidate();
        }

        public void PageRight()
        {
            _matrix.TranslatePrepend(-10, 0);
            CheckBounds();
            Invalidate();
        }

        public void MouseWheelUp()
        {
        }

        public void MouseWheelDown()
        {
        }

        public void MouseWheelLeft()
        {
        }

        public void MouseWheelRight()
        {
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
            return new Rect(new Point(0, 0), Size.Empty);
        }

        public bool CanVerticallyScroll { get; set; }
        public bool CanHorizontallyScroll { get; set; }
        public double ExtentWidth => _element.DesiredSize.Width * _matrix.M11;
        public double ExtentHeight => _element.DesiredSize.Height * _matrix.M22;
        public double ViewportWidth => ActualWidth;
        public double ViewportHeight => ActualHeight;
        public double HorizontalOffset => -_matrix.OffsetX;
        public double VerticalOffset => -_matrix.OffsetY;
        public ScrollViewer ScrollOwner { get; set; }

        #endregion
    }
}
