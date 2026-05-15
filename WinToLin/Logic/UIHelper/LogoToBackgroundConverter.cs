using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinToLin.Converters;

public class LogoToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string imagePath = value as string;

        if (string.IsNullOrWhiteSpace(imagePath))
            return new SolidColorBrush(Colors.DimGray);

        try
        {
            Uri uri;
            
            // Supports:
            // "/Assets/logo.png"
            // "Assets/logo.png"
            // "pack://application:,,,/Assets/logo.png"

            if (imagePath.StartsWith("pack://"))
            {
                uri = new Uri(imagePath, UriKind.Absolute);
            }
            else
            {
                imagePath = imagePath.TrimStart('/');

                uri = new Uri(
                    $"pack://application:,,,/{imagePath}",
                    UriKind.Absolute);
            }

            BitmapImage bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            // Convert to writable bitmap for pixel access
            FormatConvertedBitmap formatted = new FormatConvertedBitmap(
                bitmap,
                PixelFormats.Bgra32,
                null,
                0);

            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;

            int stride = width * 4;
            byte[] pixels = new byte[height * stride];

            formatted.CopyPixels(pixels, stride, 0);

            long r = 0;
            long g = 0;
            long b = 0;
            long count = 0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte blue = pixels[i];
                byte green = pixels[i + 1];
                byte red = pixels[i + 2];
                byte alpha = pixels[i + 3];

                // Ignore transparent pixels
                if (alpha < 180)
                    continue;

                // Ignore very dark pixels
                if (red < 20 && green < 20 && blue < 20)
                    continue;

                // Ignore very bright / white pixels
                if (red > 240 && green > 240 && blue > 240)
                    continue;

                // Ignore grayscale / low saturation colors
                int max = Math.Max(red, Math.Max(green, blue));
                int min = Math.Min(red, Math.Min(green, blue));

                int saturation = max - min;

                if (saturation < 25)
                    continue;

                r += red;
                g += green;
                b += blue;
                count++;
            }
            
            if (count > 0)
            {
                byte avgR = (byte)((r / count) * 0.5);
                byte avgG = (byte)((g / count) * 0.5);
                byte avgB = (byte)((b / count) * 0.5);

                return new SolidColorBrush(
                    Color.FromRgb(avgR, avgG, avgB));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return new SolidColorBrush(Colors.DimGray);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}