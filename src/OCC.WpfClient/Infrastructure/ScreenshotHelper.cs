using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OCC.WpfClient.Infrastructure
{
    public static class ScreenshotHelper
    {
        public static string CaptureWindowToBase64(Window window)
        {
            try
            {
                // Create a RenderTargetBitmap of the window
                double width = window.ActualWidth;
                double height = window.ActualHeight;

                if (width <= 0 || height <= 0) return string.Empty;

                RenderTargetBitmap bmp = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(window);

                // Convert to Base64
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    byte[] bytes = ms.ToArray();
                    return Convert.ToBase64String(bytes);
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static void ShowScreenshot(string? base64, string title = "Screenshot Preview")
        {
            if (string.IsNullOrEmpty(base64)) return;
            try
            {
                byte[] binaryData = Convert.FromBase64String(base64);
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(binaryData);
                bitmap.EndInit();

                var window = new Window
                {
                    Title = title,
                    Width = 1024,
                    Height = 768,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                    ShowInTaskbar = true
                };

                var grid = new System.Windows.Controls.Grid();
                var image = new System.Windows.Controls.Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(20)
                };
                grid.Children.Add(image);
                window.Content = grid;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing screenshot: {ex.Message}");
            }
        }
    }
}
