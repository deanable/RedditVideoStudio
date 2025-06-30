using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace RedditVideoStudio.UI.Converters
{
    public class FullPathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string path && !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : string.Empty;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}