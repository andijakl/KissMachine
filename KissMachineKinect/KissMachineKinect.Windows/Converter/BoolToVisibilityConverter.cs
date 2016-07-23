using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace KinectDemo.Converter
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var invert = false;
            if (parameter != null)
            {
                var paramString = parameter as string;
                if (paramString != null)
                {
                    if (paramString.Equals("i"))
                    {
                        invert = true;
                    }
                }
            }
            var b = (bool)value;
            if (invert) b = !b;

            if (targetType == typeof(string))
                return b.ToString();
            if (targetType == typeof(Visibility))
                return b ? Visibility.Visible : Visibility.Collapsed;

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var invert = false;
            if (parameter != null)
            {
                var paramString = parameter as string;
                if (paramString != null && paramString.Equals("i"))
                {
                    invert = true;
                }
            }

            if (value is Visibility)
            {
                var v = (Visibility)value;
                return (invert ? v != Visibility.Visible : v == Visibility.Visible);
            }

            throw new Exception("The method or operation is not implemented.");
        }
    }
}
