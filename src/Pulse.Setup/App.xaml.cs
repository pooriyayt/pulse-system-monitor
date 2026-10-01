using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Pulse.Setup
{
    public partial class App : Application
    {
        public static string[] Args { get; private set; } = new string[0];

        static bool Has(params string[] names) =>
            Args.Any(a => names.Any(n => a.Equals(n, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Silent switches kept from the original console installer (winget relies on these),
        /// plus the usual Inno/NSIS spellings so any manifest InstallerSwitches value keeps working.
        /// </summary>
        public static bool Silent => Has("/s", "-s", "--silent", "/silent", "/verysilent", "/quiet", "/q", "/qn", "/passive");

        protected override void OnStartup(StartupEventArgs e)
        {
            Args = e.Args;
            base.OnStartup(e);

            Resources["UiFont"] = Strings.Fa ? (FontFamily)Resources["Fa"] : (FontFamily)Resources["En"];

            // elevated helper pass: only registers the signing certificate, no window
            if (Has("/trustcert"))
            {
                Shutdown(Installer.TrustCertificateOnly());
                return;
            }

            // uninstaller (Programs and Features / Settings › Apps)
            // the payload-free copy (Pulse-Uninstall.exe) is always the uninstaller, even without arguments
            bool isUninstaller = !System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("app.msix");
            if (isUninstaller || Has("/uninstall", "/u"))
            {
                if (Silent)
                {
                    Task.Run(() =>
                    {
                        int code = 0;
                        try { Uninstaller.Uninstall((p, s) => Installer.Log(s)); }
                        catch (Exception ex) { Installer.Log("FAILED: " + ex.Message); code = 1; }
                        Uninstaller.ScheduleSelfDelete();
                        Dispatcher.Invoke(() => Shutdown(code));
                    });
                    return;
                }
                new UninstallWindow().Show();
                return;
            }

            // silent install (winget / scripts): no window, exit code reports the result
            if (Silent)
            {
                Task.Run(() =>
                {
                    int code = 0;
                    try { Installer.Install(true, (p, s) => Installer.Log(s)); }
                    catch (Exception ex) { Installer.Log("FAILED: " + ex.Message); code = 1; }
                    Dispatcher.Invoke(() => Shutdown(code));
                });
                return;
            }

            new MainWindow().Show();
        }
    }
}
