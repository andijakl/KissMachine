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
        private TextBlock HeartFace { get; }
        public double CircleSize { get; set; } = 90;
        public ColorSpacePoint FacePosInColor { get; set; }

        public CameraSpacePoint FacePosInCamera { get; set; }

        public PlayerInfo(Canvas drawingCanvas, ulong trackingId, int bodyNum)
        {
            BodyNum = bodyNum;
            TrackingId = trackingId;
            HeartFace = new TextBlock
            {
                Text = "\uEB52",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = CircleSize,
                Foreground = new SolidColorBrush(Colors.Red),
                Opacity = 0.5,
                Visibility = Visibility.Collapsed,
                Width = CircleSize,
                Height = CircleSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            drawingCanvas.Children.Add(HeartFace);
        }

        public void RemoveFromWorld(Canvas drawingCanvas)
        {
            drawingCanvas.Children.Remove(HeartFace);
        }

        public bool IsVisible()
        {
            return HeartFace.Visibility == Visibility.Visible;
        }

        public void SetVisibility(bool visible)
        {
            HeartFace.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetPosition(CameraSpacePoint facePosInCamera, ColorSpacePoint facePosInColor)
        {
            FacePosInCamera = facePosInCamera;
            FacePosInColor = facePosInColor;
            Canvas.SetLeft(HeartFace, facePosInColor.X - CircleSize / 2.0);
            Canvas.SetTop(HeartFace, facePosInColor.Y - CircleSize / 2.0);
        }

    }
}
