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
        /// <summary>متن «چه چیزی جدید است» از صفحه‌ی Release گیت‌هاب</summary>
        public string ReleaseNotes = "";
        /// <summary>حجم فایل نصب (بایت) یا 0</summary>
        public long SizeBytes;
        /// <summary>آدرس صفحه‌ی Release</summary>
        public string PageUrl = "";
    }

    /// <summary>
    /// چک و دانلود آپدیت از سرور اختصاصی برنامه.
    /// همه‌ی خطاها (نبود اینترنت، خرابی سرور، JSON نامعتبر) بی‌صدا خورده می‌شوند
    /// تا هیچ‌وقت عملکرد برنامه مختل نشود.
    /// </summary>
    public static class UpdateChecker
    {
        /// <summary>آخرین Release منتشرشده در مخزن گیت‌هاب برنامه (منبع رسمی و پایدار آپدیت)</summary>
        private const string ApiUrl = "https://api.github.com/repos/pooriyayt/pulse-system-monitor/releases/latest";

        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            // API گیت‌هاب بدون User-Agent درخواست را رد می‌کند
            h.DefaultRequestHeaders.UserAgent.ParseAdd("Pulse-UpdateChecker");
            h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return h;
        }

        /// <summary>نسخه‌ی فعلی برنامه (از منیفست پکیج)</summary>
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var v = Windows.ApplicationModel.Package.Current.Id.Version;
                    return v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
                }
                catch { return "2.3.1"; }
            }
        }

        /// <summary>
        /// چک آپدیت از Releaseهای گیت‌هاب — null یعنی دسترسی نبود یا پاسخ نامعتبر بود (بی‌خیال شو).
        /// تگ Release (مثل V2.3) نسخه است و فایل نصب از Assetهای همان Release برداشته می‌شود.
        /// </summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                string json = await Http.GetStringAsync(ApiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
                if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

                string tag = root.GetProperty("tag_name").GetString() ?? "";
                string latest = tag.Trim().TrimStart('v', 'V');
                if (latest.Length == 0) return null;

                // انتخاب فایل نصب: اولویت با *-Setup.exe، بعد هر exe، بعد msix
                string url = "";
                long size = 0;
                int best = int.MaxValue;
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        string name = a.GetProperty("name").GetString() ?? "";
                        int rank = name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) ? 0
                            : name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? 1
                            : name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase) ? 2
                            : int.MaxValue;
                        if (rank < best)
                        {
                            best = rank;
                            url = a.GetProperty("browser_download_url").GetString() ?? "";
                            size = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                        }
                    }
                }
                if (url.Length == 0) return null;

                return new UpdateInfo
                {
                    LatestVersion = latest,
                    DownloadUrl = url,
                    SizeBytes = size,
                    ReleaseNotes = root.TryGetProperty("body", out var body) ? (body.GetString() ?? "").Trim() : "",
                    PageUrl = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "",
                    UpdateAvailable = IsNewer(latest, CurrentVersion),
                };
            }
            catch
            {
                return null; // آفلاین یا گیت‌هاب در دسترس نیست — برنامه عادی ادامه می‌دهد
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
