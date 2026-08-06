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
$stageParent = $env:TEMP
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

# NOTE: exclude the Dependencies folder — it also contains .msix files.
$msix = Get-ChildItem $pkgDir -Recurse -Filter *.msix |
    Where-Object { $_.FullName -notmatch '\\Dependencies\\' } |
    Select-Object -First 1
if (-not $msix) { Write-Host "MSIX not found!" -ForegroundColor Red; exit 1 }

# ---- framework dependencies ----
# The app is WinUI 3, so its manifest carries a PackageDependency on
# Microsoft.WindowsAppRuntime.<ver>. A clean machine does not have it, and
# Add-AppxPackage then fails with 0x80073CF3. Ship the framework packages
# inside the installer and pass them with -DependencyPath.
$depDir = Join-Path $stageParent "TMP-installer-deps"
Remove-Item $depDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $depDir | Out-Null

# 1) Windows App SDK runtime — prefer the one MSBuild already staged next to the package.
$runtimeMsix = Get-ChildItem $pkgDir -Recurse -Filter "Microsoft.WindowsAppRuntime.*.msix" |
    Where-Object { $_.FullName -match '\\Dependencies\\(win10-)?x64\\' -and $_.Name -notmatch 'DDLM|Main|Singleton' } |
    Select-Object -First 1
if (-not $runtimeMsix) {
    # fall back to the NuGet package the project references
    $sdkVer = ([xml](Get-Content $proj)).Project.ItemGroup.PackageReference |
        Where-Object { $_.Include -eq "Microsoft.WindowsAppSDK" } | Select-Object -First 1
    $nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
    $cand = Join-Path $nuget "microsoft.windowsappsdk\$($sdkVer.Version)\tools\MSIX\win10-x64\Microsoft.WindowsAppRuntime.$(($sdkVer.Version -split '\.')[0..1] -join '.').msix"
    if (Test-Path $cand) { $runtimeMsix = Get-Item $cand }
}
if (-not $runtimeMsix) { Write-Host "WindowsAppRuntime framework package not found!" -ForegroundColor Red; exit 1 }
Copy-Item $runtimeMsix.FullName (Join-Path $depDir "dep.runtime.msix")
Write-Host "  dependency: $($runtimeMsix.Name)" -ForegroundColor DarkGray

# 2) VCLibs Desktop — the runtime itself depends on it; not present on clean Win10.
$vclibs = Get-ChildItem $pkgDir -Recurse -Filter "Microsoft.VCLibs.x64*.appx" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $vclibs) {
    $vclibs = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\Microsoft.VCLibs.Desktop" `
        -Recurse -Filter "Microsoft.VCLibs.x64.14.00.Desktop.appx" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
}
if (-not $vclibs) { Write-Host "VCLibs Desktop package not found!" -ForegroundColor Red; exit 1 }
Copy-Item $vclibs.FullName (Join-Path $depDir "dep.vclibs.appx")
Write-Host "  dependency: $($vclibs.Name)" -ForegroundColor DarkGray

# ---- stage installer files ----
$stage = Join-Path $stageParent "TMP-installer-stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item $msix.FullName (Join-Path $stage "app.msix")
Copy-Item $cerPath (Join-Path $stage "app.cer")
Copy-Item (Join-Path $root "TaskManagerPro\Assets\app.ico") (Join-Path $stage "app.ico")
Copy-Item (Join-Path $depDir "dep.runtime.msix") (Join-Path $stage "dep.runtime.msix")
Copy-Item (Join-Path $depDir "dep.vclibs.appx") (Join-Path $stage "dep.vclibs.appx")

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
    static string logFile = Path.Combine(Path.GetTempPath(), "Pulse-Setup.log");

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
        try { Console.WriteLine(msg); } catch { }
        try { File.AppendAllText(logFile, DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine); } catch { }
    }

    // Never blocks when there is no interactive console (winget, CI, redirected stdin).
    static void Pause(string msg)
    {
        if (silent) return;
        try
        {
            if (Console.IsInputRedirected) return;
            Console.WriteLine(msg);
            Console.ReadKey();
        }
        catch { }
    }

    // Runs a powershell command, returns exit code, captures all output.
    static int RunPowerShell(string command, out string output)
    {
        ProcessStartInfo ps = new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"");
        ps.UseShellExecute = false;
        ps.CreateNoWindow = true;
        ps.RedirectStandardOutput = true;
        ps.RedirectStandardError = true;
        Process p = Process.Start(ps);
        string so = p.StandardOutput.ReadToEnd();
        string se = p.StandardError.ReadToEnd();
        p.WaitForExit();
        output = (so + Environment.NewLine + se).Trim();
        return p.ExitCode;
    }

    static bool CertAlreadyTrusted(X509Certificate2 cert)
    {
        try
        {
            X509Store store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            bool found = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count > 0;
            store.Close();
            return found;
        }
        catch { return false; }
    }

    static int TrustCertificate(string cerPath)
    {
        X509Certificate2 cert = new X509Certificate2(cerPath);
        X509Store store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        store.Add(cert);
        store.Close();
        return 0;
    }

    static string StageFiles(out string msix, out string cer, out string depRuntime, out string depVcLibs)
    {
        string dir = Path.Combine(Path.GetTempPath(), "TMProSetup");
        Directory.CreateDirectory(dir);
        msix = Extract("app.msix", dir);
        cer = Extract("app.cer", dir);
        depRuntime = Extract("dep.runtime.msix", dir);
        depVcLibs = Extract("dep.vclibs.appx", dir);
        return dir;
    }

    static int Main(string[] args)
    {
        bool trustOnly = false;
        foreach (string a in args)
        {
            string al = a.ToLowerInvariant();
            if (al == "/s" || al == "--silent" || al == "/silent" || al == "-s" || al == "/quiet" || al == "/q")
                silent = true;
            if (al == "/trustcert")
            {
                trustOnly = true;
                silent = true;
            }
        }

        try { if (!silent) Console.Title = "Pulse Setup"; } catch { }

        string msix, cer, depRuntime, depVcLibs;

        // ---- elevated helper pass: only registers the signing certificate ----
        if (trustOnly)
        {
            try
            {
                StageFiles(out msix, out cer, out depRuntime, out depVcLibs);
                return TrustCertificate(cer);
            }
            catch (Exception ex)
            {
                Log("  Certificate registration failed: " + ex.Message);
                return 1;
            }
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
            Log("  Extracting files...");
            StageFiles(out msix, out cer, out depRuntime, out depVcLibs);

            // ---- trust the signing certificate (needs admin; elevate just for this) ----
            X509Certificate2 cert = new X509Certificate2(cer);
            if (!CertAlreadyTrusted(cert))
            {
                Log("  Trusting application certificate...");
                if (IsAdmin())
                {
                    TrustCertificate(cer);
                }
                else
                {
                    // Elevate a short-lived child that only touches the certificate store,
                    // so the package itself is still installed for the *current* user.
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(
                            Assembly.GetExecutingAssembly().Location, "/trustcert");
                        psi.UseShellExecute = true;
                        psi.Verb = "runas";
                        Process elevated = Process.Start(psi);
                        elevated.WaitForExit();
                        if (elevated.ExitCode != 0)
                        {
                            Log("  Could not register the signing certificate.");
                            Pause("  Press any key to close...");
                            return elevated.ExitCode;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("  Administrator access is required to install: " + ex.Message);
                        Pause("  Press any key to close...");
                        return 1;
                    }
                }
            }

            // ---- install, dependencies first ----
            // The app is WinUI 3: without -DependencyPath this fails with 0x80073CF3
            // on any machine that does not already have the Windows App Runtime.
            Log("  Installing Pulse (this may take a minute)...");
            // VCLibs is a dependency of the runtime, not of the app, so -DependencyPath
            // rejects it ("provided but not used"). Install it on its own first.
            string deps = "@('" + depRuntime + "')";
            string cmd =
                "$ErrorActionPreference='Stop'; " +
                "try { Add-AppxPackage -Path '" + depVcLibs + "' } catch { }; " +
                "try { Add-AppxPackage -Path '" + msix + "' -DependencyPath " + deps + " -ForceApplicationShutdown } " +
                "catch { Get-AppxPackage TaskManagerPro | Remove-AppxPackage -ErrorAction SilentlyContinue; " +
                "Add-AppxPackage -Path '" + msix + "' -DependencyPath " + deps + " }";

            string output;
            int code = RunPowerShell(cmd, out output);

            if (code != 0)
            {
                Log("");
                Log("  Installation FAILED:");
                Log("  " + output);
                Log("");
                Log("  Windows 10 version 1809 or newer is required.");
                Log("  Full log: " + logFile);
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
                string ignored;
                RunPowerShell(shortcutCmd, out ignored);
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
            Log("  Full log: " + logFile);
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
    /res:"$stage\dep.runtime.msix",dep.runtime.msix `
    /res:"$stage\dep.vclibs.appx",dep.vclibs.appx `
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
