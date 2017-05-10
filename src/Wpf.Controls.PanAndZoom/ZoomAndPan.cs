// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static System.Math;

namespace Wpf.Controls.PanAndZoom
{
    /// <summary>
    /// </summary>
    public class ZoomAndPan : Decorator, IScrollInfo
    {
        public static readonly DependencyProperty ZoomSpeedProperty =
            DependencyProperty.Register("ZoomSpeed", typeof(double), typeof(ZoomAndPan), new PropertyMetadata(1.2));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register("Zoom", typeof(double), typeof(ZoomAndPan), new PropertyMetadata(0.0));

        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register("MinZoom", typeof(double), typeof(ZoomAndPan), new PropertyMetadata(1.0));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register("MaxZoom", typeof(double), typeof(ZoomAndPan), new PropertyMetadata(10.0));

        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register("Background", typeof(Brush), typeof(ZoomAndPan),
                new PropertyMetadata(Brushes.Transparent, (o, args) => (o as UIElement)?.InvalidateVisual()));


        private AutoFitMode _autoFitMode = AutoFitMode.None;
        private Matrix _matrix = Matrix.Identity;
        private Point _pan;
        private Point _previous;

        static ZoomAndPan()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZoomAndPan),
                new FrameworkPropertyMetadata(typeof(ZoomAndPan)));
        }

        /// <summary>
        /// </summary>
        public ZoomAndPan()
        {
            Focusable = true;
            Unloaded += OnUnloaded;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Focus();
            PreviewMouseWheel += OnPreviewMouseWheel;
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            PreviewMouseRightButtonUp += OnPreviewMouseRightButtonUp;
            PreviewMouseMove += OnPreviewMouseMove;
            ManipulationDelta += OnManipulationDelta;
            ScrollOwner?.InvalidateScrollInfo();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PreviewMouseWheel -= OnPreviewMouseWheel;
            PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
            PreviewMouseRightButtonUp -= OnPreviewMouseRightButtonUp;
            PreviewMouseMove -= OnPreviewMouseMove;
            ManipulationDelta -= OnManipulationDelta;
        }

        /// <summary>
        /// </summary>
        public double ZoomSpeed
        {
            get { return (double) GetValue(ZoomSpeedProperty); }
            set { SetValue(ZoomSpeedProperty, value); }
        }


        public double Zoom
        {
            get { return (double) GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }


        public double MinZoom
        {
            get { return (double) GetValue(MinZoomProperty); }
            set { SetValue(MinZoomProperty, value); }
        }


        /// <summary>
        /// </summary>
        public double MaxZoom
        {
            get { return (double) GetValue(MaxZoomProperty); }
            set { SetValue(MaxZoomProperty, value); }
        }

        public Brush Background
        {
            get { return (Brush) GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        protected override Size MeasureOverride(Size constraint)
        {

            Child?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            if (double.IsPositiveInfinity(constraint.Height) || double.IsPositiveInfinity(constraint.Width))
            {
                return Child.DesiredSize;
            }

            return constraint;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {

            Child?.Arrange(new Rect(new Point(0, 0), Child.DesiredSize));

            AutoFit();

            ApplyBounds();
            Invalidate();

            return finalSize;
        }   

        private void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            var delta = e.DeltaManipulation;

            PanBy(delta.Translation);            
            ZoomBy(delta.Scale.X, e.ManipulationOrigin);
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Child == null) return;

            var point = e.GetPosition(Child);
            ZoomDeltaTo(e.Delta, point);
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Child == null) return;

            var point = e.GetPosition(Child);
            StartPan(point);
            Child.CaptureMouse();
        }

        private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Child == null) return;

            Child.ReleaseMouseCapture();
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (Child == null || !Child.IsMouseCaptured) return;

            var point = e.GetPosition(Child);
            PanTo(point);
        }


        /// <summary>
        /// </summary>
        private void Invalidate()
        {
            if (Child == null) return;

            Child.RenderTransform = new MatrixTransform(_matrix);
            Child.InvalidateVisual();
            ScrollOwner?.InvalidateScrollInfo();

            Zoom = _matrix.M11;
        }

        /// <summary>
        /// </summary>
        /// <param name="factor"></param>
        /// <param name="center"></param>
        public void ZoomBy(double factor, Point center)
        {
            var targetZoom = Zoom*factor;

            if (targetZoom > MaxZoom)
            {
                ZoomTo(MaxZoom, center);
            }
            else if (targetZoom < MinZoom)
            {
                ZoomTo(MinZoom, center);
            }
            else
            {
                _matrix.ScaleAtPrepend(factor, factor, center.X, center.Y);
                ApplyBounds();
                Invalidate();
            }
        }

        public void ZoomTo(double zoom, Point center)
        {
            var factor = zoom/Zoom;
            _matrix.ScaleAtPrepend(factor, factor, center.X, center.Y);

            ApplyBounds();
            Invalidate();
        }

        private void ApplyBounds()
        {
            if (ExtentWidth <= ViewportWidth)
            {
                _matrix.OffsetX = (ViewportWidth - ExtentWidth)/2;
            }
            else
            {
                _matrix.OffsetX = Max(Min(_matrix.OffsetX, 0), ViewportWidth - ExtentWidth);
            }

            if (ExtentHeight <= ViewportHeight)
            {
                _matrix.OffsetY = (ViewportHeight - ExtentHeight)/2;
            }
            else
            {
                _matrix.OffsetY = Max(Min(_matrix.OffsetY, 0), ViewportHeight - ExtentHeight);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="delta"></param>
        /// <param name="point"></param>
        public void ZoomDeltaTo(int delta, Point point)
        {
            DisableAutoFit();
            ZoomBy(delta > 0 ? ZoomSpeed : 1/ZoomSpeed, point);
        }

        private void DisableAutoFit()
        {
            _autoFitMode = AutoFitMode.None;
        }

        /// <summary>
        /// </summary>
        /// <param name="point"></param>
        public void StartPan(Point point)
        {
            DisableAutoFit();

            _pan = new Point();
            _previous = new Point(point.X, point.Y);
        }

        /// <summary>
        /// </summary>
        /// <param name="point"></param>
        public void PanTo(Point point)
        {
            var delta = point - _previous;
            _previous = point;

            _pan.Offset(delta.X, delta.Y);

            _matrix.TranslatePrepend(_pan.X, _pan.Y);

            ApplyBounds();
            Invalidate();
        }

        public void PanBy(Vector delta)
        {

            _matrix.Translate(delta.X, delta.Y);

            ApplyBounds();
            Invalidate();
        }

        /// <summary>
        /// </summary>
        /// <param name="panelSize"></param>
        /// <param name="elementSize"></param>
        public void Extent(Size panelSize, Size elementSize)
        {
            if (Child != null)
            {
                var pw = panelSize.Width;
                var ph = panelSize.Height;
                var ew = elementSize.Width;
                var eh = elementSize.Height;
                var zx = pw/ew;
                var zy = ph/eh;
                var zoom = Min(zx, zy);
                var cx = ew/2.0;
                var cy = eh/2.0;

                _matrix = Matrix.Identity;
                _matrix.ScaleAt(zoom, zoom, cx, cy);

                ApplyBounds();
                Invalidate();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="panelSize"></param>
        /// <param name="elementSize"></param>
        public void Fill(Size panelSize, Size elementSize)
        {
            if (Child != null)
            {
                var pw = panelSize.Width;
                var ph = panelSize.Height;
                var ew = elementSize.Width;
                var eh = elementSize.Height;
                var zx = pw/ew;
                var zy = ph/eh;
                var scale = Max(zx, zy);

                _matrix = Matrix.Identity;
                _matrix.Translate((pw - ew)/2, (ph - eh)/2);
                _matrix.ScaleAt(scale, scale, pw/2, ph/2);


                ApplyBounds();
                Invalidate();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="panelSize"></param>
        /// <param name="elementSize"></param>
        public void AutoFit(Size panelSize, Size elementSize)
        {
            if (Child != null)
            {
                switch (_autoFitMode)
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
        /// </summary>
        public void ToggleAutoFitMode()
        {
            switch (_autoFitMode)
            {
                case AutoFitMode.None:
                    _autoFitMode = AutoFitMode.Extent;
                    break;
                case AutoFitMode.Extent:
                    _autoFitMode = AutoFitMode.Fill;
                    break;
                case AutoFitMode.Fill:
                    _autoFitMode = AutoFitMode.None;
                    break;
            }
        }

        /// <summary>
        /// </summary>
        public void Reset()
        {
            _matrix = Matrix.Identity;
            DisableAutoFit();
            ApplyBounds();
            Invalidate();
        }

        /// <summary>
        /// </summary>
        public void Extent()
        {
            _autoFitMode = AutoFitMode.Extent;
            Extent(DesiredSize, Child.DesiredSize);
        }

        /// <summary>
        /// </summary>
        public void Fill()
        {
            _autoFitMode = AutoFitMode.Fill;
            Fill(DesiredSize, Child.DesiredSize);
        }

        /// <summary>
        /// </summary>
        public void AutoFit()
        {
            if (Child != null)
            {
                AutoFit(DesiredSize, Child.DesiredSize);
            }
        }


        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));
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
            _matrix.Translate(0, 10);
            ApplyBounds();
            Invalidate();
        }

        public void PageDown()
        {
            _matrix.Translate(0, -10);
            ApplyBounds();
            Invalidate();
        }

        public void PageLeft()
        {
            _matrix.Translate(10, 0);
            ApplyBounds();
            Invalidate();
        }

        public void PageRight()
        {
            _matrix.Translate(-10, 0);
            ApplyBounds();
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
        public double ExtentWidth => Child?.DesiredSize.Width*_matrix.M11 ?? 0.0;
        public double ExtentHeight => Child?.DesiredSize.Height*_matrix.M22 ?? 0.0;
        public double ViewportWidth => DesiredSize.Width;
        public double ViewportHeight => DesiredSize.Height;
        public double HorizontalOffset => -_matrix.OffsetX;
        public double VerticalOffset => -_matrix.OffsetY;
        public ScrollViewer ScrollOwner { get; set; }

        #endregion
    }
}