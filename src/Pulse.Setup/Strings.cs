using System.Globalization;

namespace Pulse.Setup
{
    /// <summary>English / Persian UI text. Persian is picked when Windows' display language is Persian.</summary>
    static class Strings
    {
        public static readonly bool Fa = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";

        static string T(string en, string fa) => Fa ? fa : en;

        public static string Caption => T("Pulse Setup", "نصب‌کننده Pulse");
        public static string Tagline => T("A beautiful system monitor for Windows", "مانیتور سیستم زیبا و مدرن برای ویندوز");
        public static string Feature1 => T("✦  Live CPU, RAM, GPU, disk and network graphs", "✦  نمودار زنده‌ی CPU، RAM، GPU، دیسک و شبکه");
        public static string Feature2 => T("✦  Processes, startup apps, services and tray graphs", "✦  پردازش‌ها، برنامه‌های استارتاپ، سرویس‌ها و نمودار Tray");
        public static string Feature3 => T("✦  7 themes, 5 languages — nothing else to install", "✦  ۷ تم و ۵ زبان — بدون نیاز به نصب پیش‌نیاز");
        public static string OptDesktop => T("Create a desktop shortcut", "ساخت میانبر روی دسکتاپ");
        public static string OptLaunch => T("Launch Pulse when setup finishes", "اجرای Pulse بعد از نصب");
        public static string MadeBy => T("Made by Pouriya Parniyan", "ساخته شده توسط Pouriya Parniyan");
        public static string Install => T("Install", "نصب");
        public static string Cancel => T("Cancel", "انصراف");
        public static string Close => T("Close", "بستن");
        public static string Installing => T("Installing Pulse…", "در حال نصب Pulse…");
        public static string DoneTitle => T("Pulse is installed 🎉", "Pulse با موفقیت نصب شد 🎉");
        public static string DoneText => T("Find Pulse in the Start menu or on your desktop.\nSome features (per-process network, some sensors) need Run as administrator.",
            "Pulse را از منوی استارت یا میانبر دسکتاپ اجرا کنید.\nبرخی امکانات (شبکه‌ی هر پردازش، بعضی سنسورها) به اجرا با دسترسی Administrator نیاز دارند.");
        public static string LaunchNow => T("Launch Pulse", "اجرای Pulse");
        public static string ErrorTitle => T("Something went wrong", "مشکلی پیش آمد");

        public static string StepExtract => T("Extracting files…", "در حال استخراج فایل‌ها…");
        public static string StepCert => T("Trusting the application certificate…", "در حال ثبت گواهی برنامه…");
        public static string StepVcLibs => T("Installing runtime libraries…", "در حال نصب کتابخانه‌های مورد نیاز…");
        public static string StepApp => T("Installing Pulse (this may take a minute)…", "در حال نصب Pulse (ممکن است کمی طول بکشد)…");
        public static string StepShortcut => T("Creating shortcuts…", "در حال ساخت میانبر…");
        public static string StepDone => T("Done", "تمام شد");

        public static string ErrCert => T("Could not register the signing certificate.", "ثبت گواهی برنامه انجام نشد.");
        public static string ErrAdmin => T("Administrator access is required to install.", "برای نصب، دسترسی Administrator لازم است.");
        public static string ErrInstall => T("Installation failed. Windows 10 version 1809 or newer is required.",
            "نصب ناموفق بود. ویندوز ۱۰ نسخه ۱۸۰۹ یا جدیدتر لازم است.");
    }
}
