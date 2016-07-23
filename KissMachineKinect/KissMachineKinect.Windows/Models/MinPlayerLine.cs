using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using KissMachineKinect.Utilities;

namespace KissMachineKinect.Models
{
    public class MinPlayerLine
    {
        private Line _line1 { get; set; }
        private Line _line2 { get; set; }

        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        private double _centerX { get; set; }
        private double _centerY { get; set; }

        public MinPlayerLine(Canvas drawingCanvas, Color lineColor)
        {
            _line1 = new Line
            {
                StrokeEndLineCap = PenLineCap.Triangle,
                StrokeThickness = 4.0,
                StrokeStartLineCap = PenLineCap.Triangle,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                StrokeDashCap = PenLineCap.Triangle,
                StrokeLineJoin = PenLineJoin.Miter,
                Stroke = new SolidColorBrush(lineColor),
                Visibility = Visibility.Visible
            };
            _line2 = new Line
            {
                StrokeEndLineCap = PenLineCap.Triangle,
                StrokeThickness = 4.0,
                StrokeStartLineCap = PenLineCap.Triangle,

                StrokeDashArray = new DoubleCollection { 4, 4 },
                StrokeDashCap = PenLineCap.Triangle,
                StrokeLineJoin = PenLineJoin.Miter,
                Stroke = new SolidColorBrush(lineColor),
                Visibility = Visibility.Visible
            };
            _line1.ApplyMarchingAntsAnimation(TimeSpan.FromSeconds(0.5));
            _line2.ApplyMarchingAntsAnimation(TimeSpan.FromSeconds(0.5));
            drawingCanvas.Children.Add(_line1);
            drawingCanvas.Children.Add(_line2);
        }

        public void SetVisibility(bool visible)
        {
            _line1.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            _line2.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetPosition(float x1, float y1, float x2, float y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            _centerX = (X1 + X2)/2.0;
            _centerY = (Y1 + Y2)/2.0;

            _line1.X1 = X1;
            _line1.Y1 = Y1;
            _line1.X2 = _centerX;
            _line1.Y2 = _centerY;

            _line2.X1 = X2;
            _line2.Y1 = Y2;
            _line2.X2 = _centerX;
            _line2.Y2 = _centerY;

        }
    }
}
