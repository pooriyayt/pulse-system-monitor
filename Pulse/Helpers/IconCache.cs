using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// کش آیکون برنامه‌ها:
    /// 1) روی ترد پس‌زمینه، آیکون هر exe استخراج و به PNG تبدیل می‌شود (Preload)
    /// 2) روی ترد UI، از همان بایت‌ها یک BitmapImage ساخته و کش می‌شود (Get)
    /// به این ترتیب هر آیکون فقط یک بار استخراج می‌شود و لیست پردازه‌ها سریع می‌ماند.
    /// </summary>
    public static class IconCache
    {
        // مسیر exe ← بایت‌های PNG (null یعنی قبلاً امتحان شد و آیکون نداشت)
        private static readonly ConcurrentDictionary<string, byte[]?> Bytes = new(StringComparer.OrdinalIgnoreCase);

        // مسیر exe ← تصویر آماده (فقط روی ترد UI استفاده می‌شود)
        private static readonly Dictionary<string, BitmapImage?> Images = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>[ترد پس‌زمینه] استخراج آیکون فایل اجرایی و تبدیل به PNG</summary>
        public static void Preload(string? path)
        {
            if (string.IsNullOrEmpty(path) || Bytes.ContainsKey(path)) return;

            byte[]? data = null;
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    using var bmp = icon.ToBitmap();
                    using var ms = new MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    data = ms.ToArray();
                }
            }
            catch
            {
                // بعضی فایل‌ها قابل دسترسی نیستند — اشکالی ندارد
            }
            Bytes[path] = data;
        }

        /// <summary>[فقط ترد UI] گرفتن تصویر آماده‌ی آیکون (یا null)</summary>
        public static ImageSource? Get(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Images.TryGetValue(path, out var cached)) return cached;

            if (!Bytes.TryGetValue(path, out var data) || data == null)
            {
                if (Bytes.ContainsKey(path)) Images[path] = null;
                return null;
            }

            var img = new BitmapImage { DecodePixelWidth = 32 };
            Images[path] = img;
            _ = SetSourceAsync(img, data); // بارگذاری غیرهمزمان؛ UI منتظر نمی‌ماند
            return img;
        }

        private static async Task SetSourceAsync(BitmapImage img, byte[] data)
        {
            try
            {
                using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                using (var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                }
                stream.Seek(0);
                await img.SetSourceAsync(stream);
            }
            catch { }
        }
    }
}
