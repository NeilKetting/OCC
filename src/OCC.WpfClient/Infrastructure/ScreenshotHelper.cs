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
    }
}
