using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using WindowsPreview.Kinect;
using Microsoft.Kinect.Face;

namespace KissMachineKinect.Models
{
    public class PlayerInfo
    {
        public ulong TrackingId { get; set; }
        public int BodyNum { get; set; }
        private Ellipse FaceCircle1 { get; set; }
        private Ellipse FaceCircle2 { get; set; }
        public double CircleSize { get; set; } = 30;
        public ColorSpacePoint FacePosInColor { get; set; }

        public CameraSpacePoint FacePosInCamera { get; set; }

        public PlayerInfo(Canvas drawingCanvas, ulong trackingId, int bodyNum)
        {
            BodyNum = bodyNum;
            TrackingId = trackingId;
            FaceCircle1 = new Ellipse
            {
                Fill = new SolidColorBrush(Colors.Red),
                Opacity = 0.2,
                Visibility = Visibility.Collapsed,
                Width = CircleSize,
                Height = CircleSize
            };
            FaceCircle2 = new Ellipse
            {
                Fill = new SolidColorBrush(Colors.Red),
                Opacity = 0.5,
                Visibility = Visibility.Collapsed,
                Width = CircleSize / 2.0,
                Height = CircleSize / 2.0
            };
            drawingCanvas.Children.Add(FaceCircle1);
            drawingCanvas.Children.Add(FaceCircle2);
        }

        public void RemoveFromWorld(Canvas drawingCanvas)
        {
            drawingCanvas.Children.Remove(FaceCircle1);
            drawingCanvas.Children.Remove(FaceCircle2);
        }

        public bool IsVisible()
        {
            return FaceCircle1.Visibility == Visibility.Visible;
        }

        public void SetVisibility(bool visible)
        {
            FaceCircle1.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            FaceCircle2.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            //FaceCircle.Visibility = Visibility.Collapsed;
        }

        public void SetPosition(CameraSpacePoint facePosInCamera, ColorSpacePoint facePosInColor)
        {
            FacePosInCamera = facePosInCamera;
            FacePosInColor = facePosInColor;
            Canvas.SetLeft(FaceCircle1, facePosInColor.X - CircleSize / 2.0);
            Canvas.SetTop(FaceCircle1, facePosInColor.Y - CircleSize / 2.0);
            Canvas.SetLeft(FaceCircle2, facePosInColor.X - CircleSize / 4.0);
            Canvas.SetTop(FaceCircle2, facePosInColor.Y - CircleSize / 4.0);
        }

    }
}
