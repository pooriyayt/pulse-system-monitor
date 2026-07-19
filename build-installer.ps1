# ============================================================
#  Pulse — Installer Builder
#  Builds the latest version in Release, signs it,
#  and produces a single setup exe (Pulse-<ver>-Setup.exe)
#  in the Installer folder. Share only that one file.
#
#  Run: right-click > Run with PowerShell  (or: .\build-installer.ps1)
# ============================================================
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "TaskManagerPro\TaskManagerPro.csproj"
$outDir = Join-Path $root "Installer"
New-Item -ItemType Directory -Force $outDir | Out-Null

# ---- version from manifest ----
[xml]$manifest = Get-Content (Join-Path $root "TaskManagerPro\Package.appxmanifest")
$version = $manifest.Package.Identity.Version   # e.g. 1.7.0.0
$shortVer = ($version -split '\.')[0..1] -join '.'
Write-Host "Building Pulse $shortVer ..." -ForegroundColor Cyan

# ---- signing certificate (create if missing) ----
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=TaskManagerPro" } | Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating signing certificate..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=TaskManagerPro" -KeyUsage DigitalSignature `
        -FriendlyName "Task Manager Pro Signing" -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
}
$cerPath = Join-Path $env:TEMP "TaskManagerPro.cer"
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

# ---- build signed MSIX package ----
$pkgDir = Join-Path $env:TEMP "TMP-msix-build"
Remove-Item $pkgDir -Recurse -Force -ErrorAction SilentlyContinue
dotnet build $proj -c Release -p:Platform=x64 `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageDir="$pkgDir\" `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxBundle=Never `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateThumbprint=$($cert.Thumbprint) `
    -v:m -nologo
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }

$msix = Get-ChildItem $pkgDir -Recurse -Filter *.msix | Select-Object -First 1
if (-not $msix) { Write-Host "MSIX not found!" -ForegroundColor Red; exit 1 }

# ---- stage installer files ----
$stage = Join-Path $env:TEMP "TMP-installer-stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item $msix.FullName (Join-Path $stage "app.msix")
Copy-Item $cerPath (Join-Path $stage "app.cer")
Copy-Item (Join-Path $root "TaskManagerPro\Assets\app.ico") (Join-Path $stage "app.ico")

# ---- installer source (real exe — msix and cert embedded inside) ----
# compiled with Windows built-in csc (.NET Framework 4.8); runs on all Windows 10/11 machines.
$installerCs = Join-Path $env:TEMP "TMProInstaller.cs"
@'
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

[assembly: AssemblyTitle("Pulse Setup")]
[assembly: AssemblyDescription("Pulse - System Monitor Installer")]
[assembly: AssemblyProduct("Pulse")]
[assembly: AssemblyCompany("Pouriya Parniyan")]
[assembly: AssemblyCopyright("(c) Pouriya Parniyan - pouriyaparniyan.ir")]
[assembly: AssemblyVersion("__VER__")]
[assembly: AssemblyFileVersion("__VER__")]
[assembly: AssemblyInformationalVersion("__SHORTVER__")]

static class Setup
{
    static bool silent = false;

    static bool IsAdmin()
    {
        try
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    static string Extract(string resName, string dir)
    {
        string path = Path.Combine(dir, resName);
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
        using (FileStream f = File.Create(path))
            s.CopyTo(f);
        return path;
    }

    static void Log(string msg)
    {
        Console.WriteLine(msg);
    }

    static void Pause(string msg)
    {
        if (silent) return;
        Console.WriteLine(msg);
        Console.ReadKey();
    }

    static int Main(string[] args)
    {
        foreach (string a in args)
        {
            string al = a.ToLowerInvariant();
            if (al == "/s" || al == "--silent" || al == "/silent" || al == "-s")
                silent = true;
        }

        if (!silent)
        {
            Console.Title = "Pulse Setup";
        }

        if (!IsAdmin())
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                ProcessStartInfo psi = new ProcessStartInfo(location,
                    silent ? "/S" : "");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process elevated = Process.Start(psi);
                if (silent && elevated != null)
                {
                    elevated.WaitForExit();
                    return elevated.ExitCode;
                }
            }
            catch
            {
                if (!silent)
                {
                    Console.WriteLine("Administrator access is required to install.");
                    Console.ReadKey();
                }
                return 1;
            }
            return 0;
        }

        if (!silent)
        {
            Console.WriteLine();
            Console.WriteLine("  ==============================================");
            Console.WriteLine("        Pulse  -  System Monitor  -  Installer");
            Console.WriteLine("  ==============================================");
            Console.WriteLine();
        }

        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "TMProSetup");
            Directory.CreateDirectory(dir);

            Log("  Extracting files...");
            string msix = Extract("app.msix", dir);
            string cer = Extract("app.cer", dir);

            Log("  Trusting application certificate...");
            X509Certificate2 cert = new X509Certificate2(cer);
            X509Store store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            store.Close();

            Log("  Installing Pulse (this may take a minute)...");
            string cmd =
                "try { Add-AppxPackage -Path '" + msix + "' -ForceApplicationShutdown -ErrorAction Stop } " +
                "catch { Get-AppxPackage TaskManagerPro | Remove-AppxPackage; Add-AppxPackage -Path '" + msix + "' -ErrorAction Stop }";

            ProcessStartInfo ps = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"" + cmd + "\"");
            ps.UseShellExecute = false;
            ps.CreateNoWindow = true;
            ps.RedirectStandardError = true;
            Process p = Process.Start(ps);
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Log("");
                Log("  Installation FAILED:");
                Log("  " + err.Trim());
                Log("");
                Log("  Windows 10 version 1809 or newer is required.");
                Pause("  Press any key to close...");
                return 1;
            }

            try
            {
                Log("  Creating desktop shortcut...");
                string iconDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Pulse");
                Directory.CreateDirectory(iconDir);
                string iconPath = Path.Combine(iconDir, "Pulse.ico");
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                using (FileStream f = File.Create(iconPath))
                    s.CopyTo(f);

                string shortcutCmd =
                    "$pkg = Get-AppxPackage TaskManagerPro; " +
                    "$lnk = (New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine($env:PUBLIC, 'Desktop', 'Pulse.lnk')); " +
                    "$lnk.TargetPath = 'explorer.exe'; " +
                    "$lnk.Arguments = ('shell:AppsFolder\\' + $pkg.PackageFamilyName + '!App'); " +
                    "$lnk.IconLocation = '" + iconPath + ",0'; " +
                    "$lnk.Description = 'Pulse - System Monitor'; " +
                    "$lnk.Save()";
                ProcessStartInfo sc = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"" + shortcutCmd + "\"");
                sc.UseShellExecute = false;
                sc.CreateNoWindow = true;
                Process.Start(sc).WaitForExit();
            }
            catch { }

            if (!silent)
            {
                Console.WriteLine();
                Console.WriteLine("  ==============================================");
                Console.WriteLine("   Pulse installed successfully!");
                Console.WriteLine("   A shortcut was added to your desktop.");
                Console.WriteLine("  ==============================================");
                Console.WriteLine();
            }
            Pause("  Press any key to close...");
            return 0;
        }
        catch (Exception ex)
        {
            Log("");
            Log("  Installation FAILED: " + ex.Message);
            Pause("  Press any key to close...");
            return 1;
        }
    }
}
'@ -replace '__VER__', $version -replace '__SHORTVER__', $shortVer | Set-Content $installerCs -Encoding UTF8

# ---- compile single exe ----
$setupExe = Join-Path $outDir "Pulse-$shortVer-Setup.exe"
Remove-Item $setupExe -Force -ErrorAction SilentlyContinue

$icon = Join-Path $root "TaskManagerPro\Assets\app.ico"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:exe /platform:anycpu /out:"$setupExe" `
    /win32icon:"$icon" `
    /res:"$stage\app.msix",app.msix `
    /res:"$stage\app.cer",app.cer `
    /res:"$stage\app.ico",app.ico `
    "$installerCs"
if ($LASTEXITCODE -ne 0) { Write-Host "csc compile failed" -ForegroundColor Red; exit 1 }

if (Test-Path $setupExe) {
    $mb = [Math]::Round((Get-Item $setupExe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "DONE!  $setupExe  ($mb MB)" -ForegroundColor Green
    Write-Host "Share this single file - users just run it." -ForegroundColor Green
} else {
    Write-Host "Failed to produce the exe." -ForegroundColor Red
    exit 1
}
