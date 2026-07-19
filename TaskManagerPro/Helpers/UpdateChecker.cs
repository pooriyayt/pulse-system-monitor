using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TaskManagerPro.Helpers
{
    /// <summary>نتیجه‌ی چک آپدیت</summary>
    public class UpdateInfo
    {
        public string LatestVersion = "";
        public string DownloadUrl = "";
        public bool UpdateAvailable;
    }

    /// <summary>
    /// چک و دانلود آپدیت از سرور اختصاصی برنامه.
    /// همه‌ی خطاها (نبود اینترنت، خرابی سرور، JSON نامعتبر) بی‌صدا خورده می‌شوند
    /// تا هیچ‌وقت عملکرد برنامه مختل نشود.
    /// </summary>
    public static class UpdateChecker
    {
        private const string ApiUrl = "https://api.wl-std.com/TSP/version.php";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

        /// <summary>نسخه‌ی فعلی برنامه (از منیفست پکیج)</summary>
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var v = Windows.ApplicationModel.Package.Current.Id.Version;
                    return $"{v.Major}.{v.Minor}";
                }
                catch { return "1.8"; }
            }
        }

        /// <summary>
        /// چک آپدیت از سرور — null یعنی دسترسی نبود یا پاسخ نامعتبر بود (بی‌خیال شو).
        /// </summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                string json = await Http.GetStringAsync(ApiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("status").GetString() != "success") return null;

                var data = root.GetProperty("data");
                string latest = data.GetProperty("version").GetString() ?? "";
                string url = data.GetProperty("latest_version_download_link").GetString() ?? "";
                if (latest.Length == 0 || url.Length == 0) return null;

                return new UpdateInfo
                {
                    LatestVersion = latest,
                    DownloadUrl = url,
                    UpdateAvailable = IsNewer(latest, CurrentVersion),
                };
            }
            catch
            {
                return null; // آفلاین یا سرور در دسترس نیست — برنامه عادی ادامه می‌دهد
            }
        }

        /// <summary>مقایسه‌ی نسخه‌ها ("1.6" > "1.5")</summary>
        private static bool IsNewer(string latest, string current)
        {
            try
            {
                return Version.Parse(Normalize(latest)) > Version.Parse(Normalize(current));
            }
            catch { return false; }

            static string Normalize(string v) => v.Contains('.') ? v : v + ".0";
        }

        /// <summary>
        /// دانلود فایل نسخه‌ی جدید در پوشه‌ی Temp — مسیر فایل را برمی‌گرداند
        /// یا null اگر دانلود شکست خورد.
        /// </summary>
        public static async Task<string?> DownloadAsync(UpdateInfo info, IProgress<double>? progress = null)
        {
            try
            {
                using var resp = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();

                // اسم فایل از URL یا هدر؛ در نبودش اسم پیش‌فرض
                string name = Path.GetFileName(new Uri(info.DownloadUrl).LocalPath);
                if (string.IsNullOrWhiteSpace(name) || !name.Contains('.'))
                    name = $"TaskManagerPro-{info.LatestVersion}.msix";

                string path = Path.Combine(Path.GetTempPath(), name);

                long total = resp.Content.Headers.ContentLength ?? -1;
                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var dst = File.Create(path);

                var buf = new byte[81920];
                long done = 0;
                int read;
                while ((read = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, read));
                    done += read;
                    if (total > 0) progress?.Report((double)done / total * 100.0);
                }

                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
