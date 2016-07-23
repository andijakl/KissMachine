using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace KissMachineKinect.Converter
{
    public class CountdownIntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is int)) return "Fehler";
            var countdownVal = (int)value;
            if (countdownVal == 99) return "Gebt euch ein Bussi!";
            return countdownVal > 0 ? countdownVal.ToString() : "Bussi!";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
