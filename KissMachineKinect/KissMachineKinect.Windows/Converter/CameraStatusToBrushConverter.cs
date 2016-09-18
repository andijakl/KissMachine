using System;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using KissMachineKinect.Services;

namespace KissMachineKinect.Converter
{
    public class CameraStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var camStatus = value as SonyCameraService.CameraStatusValues? ?? SonyCameraService.CameraStatusValues.NotConnected;
            Color statusColor;
            switch (camStatus)
            {
                case SonyCameraService.CameraStatusValues.NotConnected:
                    statusColor = Colors.Red;
                    break;
                case SonyCameraService.CameraStatusValues.Searching:
                    statusColor = Colors.Yellow;
                    break;
                case SonyCameraService.CameraStatusValues.Connected:
                    statusColor = Colors.Green;
                    break;
                case SonyCameraService.CameraStatusValues.RecMode:
                    statusColor = Colors.Blue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return new SolidColorBrush(statusColor);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
