using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;

class Program
{
    static SerialPort serialPort;

    // P/Invoke для получения размеров экрана
    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")]
    static extern uint GetDeviceCaps(IntPtr hdc, int nIndex);

    const int HORZRES = 8;
    const int VERTRES = 10;

    static void Main(string[] args)
    {
        string portName = "COM5"; // Укажите правильный COM-порт на Windows
        int baudRate = 9600;

        serialPort = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 5000
        };

        serialPort.Open();
        Console.WriteLine("Connected to Arduino");

        // Подписываемся на системные события
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        while (true)
        {
            string screenshotPath = CaptureScreenshot();

            if (!string.IsNullOrEmpty(screenshotPath))
            {
                var colors = GetBrightColors(screenshotPath); // Получаем два ярких цвета

                // Формируем JSON объект
                var pixelData = new { pixels = colors.Select(c => new { r = c.R, g = c.G, b = c.B }).ToArray() };

                // Сериализация JSON
                string json = JsonSerializer.Serialize(pixelData);
                Console.WriteLine($"Sending JSON: {json}");

                // Отправляем JSON на Arduino целиком
                serialPort.WriteLine(json); // Отправляем JSON на Arduino

                // Удаляем скриншот после использования
                File.Delete(screenshotPath);

                // Задержка перед следующей итерацией
                Thread.Sleep(600); // 600 миллисекундная задержка между отправками
            }
        }
    }

    static string CaptureScreenshot()
    {
        string path = Path.Combine(Path.GetTempPath(), "screenshot.png");

        // Получаем размеры экрана
        IntPtr hdc = GetDC(IntPtr.Zero);
        int screenWidth = (int)GetDeviceCaps(hdc, HORZRES);
        int screenHeight = (int)GetDeviceCaps(hdc, VERTRES);
        ReleaseDC(IntPtr.Zero, hdc);

        // Захват экрана
        Rectangle bounds = new Rectangle(0, 0, screenWidth, screenHeight);
        using (Bitmap bitmap = new Bitmap(screenWidth, screenHeight))
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }
            bitmap.Save(path, ImageFormat.Png);
        }

        return path;
    }

    static Color[] GetBrightColors(string imagePath)
    {
        using (Bitmap bitmap = new Bitmap(imagePath))
        {
            int widthSegment = bitmap.Width / 10;
            int height = bitmap.Height;

            // Цвета для правой и левой нижней частей экрана
            Color leftColor = GetBrightestColor(bitmap, 0, height - 10, widthSegment, 10);
            Color rightColor = GetBrightestColor(bitmap, bitmap.Width - widthSegment, height - 10, widthSegment, 10);

            return new[] { leftColor, rightColor };
        }
    }

    static Color GetBrightestColor(Bitmap bitmap, int x, int y, int width, int height)
    {
        Color brightestColor = Color.Black;
        double maxBrightness = 0.0;

        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                Color pixelColor = bitmap.GetPixel(i, j);
                double brightness = pixelColor.GetBrightness();

                if (brightness > maxBrightness)
                {
                    brightestColor = pixelColor;
                    maxBrightness = brightness;
                }
            }
        }

        return brightestColor;
    }

    static void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        Console.WriteLine("Session is ending, turning off the LED strip...");
        TurnOffLedStrip();
    }

    static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            Console.WriteLine("System is suspending, turning off the LED strip...");
            TurnOffLedStrip();
        }
        else if (e.Mode == PowerModes.Resume)
        {
            Console.WriteLine("System is resuming, LED strip will be reinitialized.");
            ReinitializeLedStrip();
        }
    }

    static void TurnOffLedStrip()
    {
        // Отправляем команду для отключения ленты
        serialPort.WriteLine("{\"pixels\":[{\"r\":0,\"g\":0,\"b\":0},{\"r\":0,\"g\":0,\"b\":0}]}");
    }

    static void ReinitializeLedStrip()
    {
        // Можете добавить логику для повторной инициализации, если необходимо
    }
}