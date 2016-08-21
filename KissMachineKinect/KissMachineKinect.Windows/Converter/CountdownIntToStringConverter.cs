using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using KissMachineKinect.Services;

namespace KissMachineKinect.Converter
{
    public class CountdownIntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is int)) return "Fehler";
            return KissCountdownStatusService.ConvertCodeToText((int) value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
