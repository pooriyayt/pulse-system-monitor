using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// محل ذخیره‌ی تنظیمات: در نسخه‌ی نصب‌شده (MSIX) همان LocalSettings ویندوز؛
    /// اگر برنامه بدون پکیج اجرا شود (LocalSettings در دسترس نیست) یک فایل JSON در
    /// %LOCALAPPDATA%\Pulse — تا هیچ تنظیمی بی‌صدا گم نشود.
    /// </summary>
    public static class SettingsStore
    {
        private static IDictionary<string, object>? _values;

        public static IDictionary<string, object> Values => _values ??= Open();

        private static IDictionary<string, object> Open()
        {
            try
            {
                return Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            }
            catch
            {
                return new FileBackedValues();
            }
        }

        /// <summary>دیکشنری‌ای که با هر تغییر روی فایل JSON ذخیره می‌شود (int / bool / string)</summary>
        private sealed class FileBackedValues : Dictionary<string, object>, IDictionary<string, object>
        {
            private static readonly string FilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse", "settings.json");

            public FileBackedValues()
            {
                try
                {
                    if (!File.Exists(FilePath)) return;
                    using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        object? v = p.Value.ValueKind switch
                        {
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Number => p.Value.GetInt32(),
                            JsonValueKind.String => p.Value.GetString(),
                            _ => null,
                        };
                        if (v != null) base[p.Name] = v;
                    }
                }
                catch { }
            }

            object IDictionary<string, object>.this[string key]
            {
                get => base[key];
                set { base[key] = value; Save(); }
            }

            private void Save()
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    File.WriteAllText(FilePath, JsonSerializer.Serialize<Dictionary<string, object>>(this));
                }
                catch { }
            }
        }
    }
}
