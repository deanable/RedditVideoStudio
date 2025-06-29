using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace RedditVideoStudio.UI.Converters
{
    /// <summary>
    /// A WPF value converter that converts a full file path string into just the file name.
    /// If the path is null or empty, it returns a placeholder string.
    /// </summary>
    public class FullPathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path);
            }
            return "No file selected";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
