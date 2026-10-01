using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace Pulse.Setup
{
    /// <summary>
    /// Installs the signed MSIX package. Same sequence as the original console installer
    /// (trust cert -> VCLibs -> MSIX + Windows App Runtime), so winget and every command line keep working.
    /// </summary>
    static class Installer
    {
        public const string PackageName = "Pulse";
        public static readonly string LogFile = Path.Combine(Path.GetTempPath(), "Pulse-Setup.log");

        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Build > 0 ? v.Major + "." + v.Minor + "." + v.Build : v.Major + "." + v.Minor;
            }
        }

        public static void Log(string msg)
        {
            try { File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine); } catch { }
        }

        public static bool IsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        static string Extract(string resName, string dir)
        {
            var path = Path.Combine(dir, resName);
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
            {
                if (s == null) throw new InvalidOperationException("Missing embedded file: " + resName);
                using (var f = File.Create(path)) s.CopyTo(f);
            }
            return path;
        }

        static string StageDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "PulseSetup");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ---- certificate ----

        static bool CertAlreadyTrusted(X509Certificate2 cert)
        {
            try
            {
                var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                bool found = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count > 0;
                store.Close();
                return found;
            }
            catch { return false; }
        }

        static void TrustCertificate(string cerPath)
        {
            var cert = new X509Certificate2(cerPath);
            var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            store.Close();
        }

        /// <summary>Elevated helper pass (/trustcert): only registers the signing certificate.</summary>
        public static int TrustCertificateOnly()
        {
            try
            {
                TrustCertificate(Extract("app.cer", StageDir()));
                return 0;
            }
            catch (Exception ex)
            {
                Log("Certificate registration failed: " + ex.Message);
                return 1;
            }
        }

        // ---- powershell ----

        public static int RunPowerShell(string command, out string output)
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                var se = new StringBuilder();
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) se.AppendLine(e.Data); };
                p.BeginErrorReadLine();
                var so = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                output = (so + Environment.NewLine + se).Trim();
                return p.ExitCode;
            }
        }

        // ---- install ----

        /// <summary>Runs the whole install. Throws with a readable message on failure.</summary>
        public static void Install(bool desktopShortcut, Action<double, string> progress)
        {
            progress(0.05, Strings.StepExtract);
            var dir = StageDir();
            var msix = Extract("app.msix", dir);
            var cer = Extract("app.cer", dir);
            var depRuntime = Extract("dep.runtime.msix", dir);
            var depVcLibs = Extract("dep.vclibs.appx", dir);

            // trust the signing certificate (needs admin; elevate a short-lived child just for this)
            progress(0.15, Strings.StepCert);
            var cert = new X509Certificate2(cer);
            if (!CertAlreadyTrusted(cert))
            {
                if (IsAdmin())
                {
                    TrustCertificate(cer);
                }
                else
                {
                    try
                    {
                        var psi = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "/trustcert")
                        {
                            UseShellExecute = true,
                            Verb = "runas",
                        };
                        using (var p = Process.Start(psi))
                        {
                            p.WaitForExit();
                            if (p.ExitCode != 0) throw new InvalidOperationException(Strings.ErrCert);
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        throw new InvalidOperationException(Strings.ErrAdmin);
                    }
                }
            }

            // VCLibs is a dependency of the runtime, not of the app, so -DependencyPath rejects it. Install separately.
            progress(0.30, Strings.StepVcLibs);
            string output;
            RunPowerShell("try { Add-AppxPackage -Path '" + depVcLibs + "' } catch { }", out output);

            // The app is WinUI 3: without -DependencyPath this fails with 0x80073CF3 on machines lacking the runtime.
            progress(0.45, Strings.StepApp);
            var deps = "@('" + depRuntime + "')";
            var cmd =
                "$ErrorActionPreference='Stop'; " +
                "try { Add-AppxPackage -Path '" + msix + "' -DependencyPath " + deps + " -ForceApplicationShutdown } " +
                "catch { Get-AppxPackage TaskManagerPro | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                "Get-AppxPackage " + PackageName + " | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                "Add-AppxPackage -Path '" + msix + "' -DependencyPath " + deps + " }";
            int code = RunPowerShell(cmd, out output);
            if (code != 0)
            {
                Log("Install failed: " + output);
                throw new InvalidOperationException(Strings.ErrInstall + "\n" + Trim(output) + "\n" + LogFile);
            }

            progress(0.85, Strings.StepShortcut);
            if (desktopShortcut) CreateShortcut();

            // Programs and Features entry + graphical uninstaller
            progress(0.93, Strings.StepRegister);
            Uninstaller.Register(Version);

            progress(1.0, Strings.StepFinalizing);
            try { Directory.Delete(dir, true); } catch { }
        }

        static string Trim(string s) => s != null && s.Length > 600 ? s.Substring(0, 600) + "…" : s;

        static void CreateShortcut()
        {
            try
            {
                var iconDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Pulse");
                Directory.CreateDirectory(iconDir);
                var iconPath = Path.Combine(iconDir, "Pulse.ico");
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                using (var f = File.Create(iconPath))
                    s.CopyTo(f);

                // public desktop needs admin; fall back to the current user's desktop
                var cmd =
                    "$pkg = Get-AppxPackage " + PackageName + "; " +
                    "$sh = New-Object -ComObject WScript.Shell; " +
                    "$paths = @([IO.Path]::Combine($env:PUBLIC, 'Desktop', 'Pulse.lnk'), [IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Pulse.lnk')); " +
                    "foreach ($p in $paths) { try { " +
                    "$lnk = $sh.CreateShortcut($p); $lnk.TargetPath = 'explorer.exe'; " +
                    "$lnk.Arguments = ('shell:AppsFolder\\' + $pkg.PackageFamilyName + '!App'); " +
                    "$lnk.IconLocation = '" + iconPath + ",0'; $lnk.Description = 'Pulse - System Monitor'; $lnk.Save(); " +
                    "if (Test-Path $p) { break } } catch { } }";
                string ignored;
                RunPowerShell(cmd, out ignored);
            }
            catch { }
        }

        public static void Launch()
        {
            try
            {
                string pfn;
                RunPowerShell("(Get-AppxPackage " + PackageName + ").PackageFamilyName", out pfn);
                pfn = pfn.Trim();
                if (pfn.Length == 0) return;
                Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + pfn + "!App") { UseShellExecute = true });
            }
            catch { }
        }
    }
}
