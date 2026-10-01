using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Pulse.Setup
{
    /// <summary>
    /// Programs and Features entry + the uninstall itself.
    /// The MSIX only shows in Settings › Apps, so setup also writes a classic Uninstall registry key (per user,
    /// no admin needed) pointing at a small copy of this exe that runs with /uninstall.
    /// </summary>
    static class Uninstaller
    {
        const string ArpKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Pulse";
        const string ExeName = "Pulse-Uninstall.exe";

        public static string InstallDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Pulse");

        static string UninstallExe => Path.Combine(InstallDir, ExeName);

        // ============================== register ==============================

        /// <summary>Copies the uninstaller next to the icon and writes the Programs and Features entry.</summary>
        public static void Register(string version)
        {
            try
            {
                Directory.CreateDirectory(InstallDir);

                // the payload-free build is embedded as "uninstall.exe"; dev builds without it fall back to this exe
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("uninstall.exe"))
                {
                    if (s != null)
                        using (var f = File.Create(UninstallExe)) s.CopyTo(f);
                    else
                        File.Copy(asm.Location, UninstallExe, true);
                }

                var icon = Path.Combine(InstallDir, "Pulse.ico");
                using (var s = asm.GetManifestResourceStream("app.ico"))
                using (var f = File.Create(icon)) s.CopyTo(f);

                using (var k = Registry.CurrentUser.CreateSubKey(ArpKey))
                {
                    k.SetValue("DisplayName", "Pulse");
                    k.SetValue("DisplayVersion", version);
                    k.SetValue("Publisher", "Pouriya Parniyan");
                    k.SetValue("DisplayIcon", icon);
                    k.SetValue("InstallLocation", InstallDir);
                    k.SetValue("UninstallString", "\"" + UninstallExe + "\" /uninstall");
                    k.SetValue("QuietUninstallString", "\"" + UninstallExe + "\" /uninstall /s");
                    k.SetValue("URLInfoAbout", "https://pouriyaparniyan.ir");
                    k.SetValue("HelpLink", "https://github.com/pooriyayt/pulse-system-monitor");
                    k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    k.SetValue("EstimatedSize", EstimateSizeKb(), RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Installer.Log("Uninstall registration failed: " + ex.Message);
            }
        }

        static int EstimateSizeKb()
        {
            try
            {
                string loc;
                Installer.RunPowerShell("(Get-AppxPackage " + Installer.PackageName + ").InstallLocation", out loc);
                loc = loc.Trim();
                long bytes = 0;
                if (Directory.Exists(loc))
                    foreach (var f in Directory.EnumerateFiles(loc, "*", SearchOption.AllDirectories))
                        try { bytes += new FileInfo(f).Length; } catch { }
                return (int)Math.Min(int.MaxValue, bytes / 1024);
            }
            catch { return 0; }
        }

        // ============================== uninstall ==============================

        /// <summary>Removes Pulse completely. Throws with a readable message on failure.</summary>
        public static void Uninstall(Action<double, string> progress)
        {
            progress(0.08, Strings.UStepClose);
            foreach (var p in Process.GetProcessesByName("TaskManagerPro"))
                using (p) { try { p.Kill(); p.WaitForExit(3000); } catch { } }

            progress(0.25, Strings.UStepPackage);
            string output;
            int code = Installer.RunPowerShell(
                "$p = Get-AppxPackage " + Installer.PackageName + "; if ($p) { $p | Remove-AppxPackage -ErrorAction Stop }",
                out output);
            if (code != 0)
            {
                Installer.Log("Uninstall failed: " + output);
                throw new InvalidOperationException(Strings.ErrUninstall + "\n" + output);
            }

            progress(0.70, Strings.UStepShortcuts);
            foreach (var desk in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            })
            {
                try { File.Delete(Path.Combine(desk, "Pulse.lnk")); } catch { }
            }
            try
            {
                Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Pulse"), true);
            }
            catch { }

            progress(0.88, Strings.UStepRegistry);
            try { Registry.CurrentUser.DeleteSubKeyTree(ArpKey, false); } catch { }

            progress(1.0, Strings.UStepFinalizing);
        }

        /// <summary>
        /// The running exe lives inside InstallDir and cannot delete itself:
        /// a hidden cmd waits for this process to exit and removes the folder.
        /// </summary>
        public static void ScheduleSelfDelete()
        {
            try
            {
                var dir = InstallDir;
                if (!Directory.Exists(dir)) return;
                Process.Start(new ProcessStartInfo("cmd.exe",
                    "/c ping 127.0.0.1 -n 4 > nul & rmdir /s /q \"" + dir + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetTempPath(),
                });
            }
            catch { }
        }
    }
}
