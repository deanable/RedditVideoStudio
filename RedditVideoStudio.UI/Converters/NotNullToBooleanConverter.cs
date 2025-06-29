using System;
using System.Globalization;
using System.Windows.Data;

namespace RedditVideoStudio.UI.Converters
{
    /// <summary>
    /// A WPF value converter that converts an object to a boolean.
    /// Returns true if the object is not null, and false if it is null.
    /// </summary>
    public class NotNullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}