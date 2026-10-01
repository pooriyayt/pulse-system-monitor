using System.Security.Principal;

namespace TaskManagerPro.Helpers
{
    /// <summary>تشخیص اینکه برنامه با دسترسی Administrator اجرا شده یا نه</summary>
    public static class AdminHelper
    {
        /// <summary>true یعنی برنامه Run as administrator اجرا شده است</summary>
        public static bool IsAdmin { get; } = Check();

        private static bool Check()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
