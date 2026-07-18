using System;
using Windows.UI;

namespace TaskManagerPro.Helpers
{
    /// <summary>ابزارهای رنگ: تبدیل هگز + روشن/تیره کردن</summary>
    public static class ColorUtil
    {
        public static Color FromHex(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 3) // #RGB
                    hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                if (hex.Length == 8) // #AARRGGBB — آلفا نادیده گرفته می‌شود
                    hex = hex.Substring(2);
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch { }

            // رنگ پیش‌فرض: آبی
            return Color.FromArgb(255, 97, 175, 254);
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>
        /// روشن/تیره کردن رنگ.
        /// amount مثبت (0 تا 1) = روشن‌تر، منفی (0 تا -1) = تیره‌تر
        /// </summary>
        public static Color Shift(Color c, double amount)
        {
            byte Ch(byte v) => (byte)Math.Clamp(
                amount >= 0 ? v + (255 - v) * amount : v * (1 + amount), 0, 255);
            return Color.FromArgb(255, Ch(c.R), Ch(c.G), Ch(c.B));
        }
    }
}
