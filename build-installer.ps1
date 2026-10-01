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
$proj = Join-Path $root "Pulse\Pulse.csproj"
$outDir = Join-Path $root "Installer"
$stageParent = $env:TEMP
New-Item -ItemType Directory -Force $outDir | Out-Null

# ---- version from manifest ----
[xml]$manifest = Get-Content (Join-Path $root "Pulse\Package.appxmanifest")
$version = $manifest.Package.Identity.Version   # e.g. 2.3.1.0
$parts = $version -split '\.'
$shortVer = if ($parts[2] -ne '0') { "$($parts[0]).$($parts[1]).$($parts[2])" } else { "$($parts[0]).$($parts[1])" }
# installer version
$installerVer = "2.3.1"
Write-Host "Building Pulse $shortVer, installer $installerVer ..." -ForegroundColor Cyan

# ---- signing certificate (create if missing) ----
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=Pouriya Parniyan" } | Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating signing certificate..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Pouriya Parniyan" -KeyUsage DigitalSignature `
        -FriendlyName "Pulse Signing" -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
}
$cerPath = Join-Path $env:TEMP "Pulse.cer"
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
if (-not $vclibs) {
    # fallback: copy kept next to this script (Microsoft-signed, from aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx)
    $vclibs = Get-ChildItem (Join-Path $root "tools\deps") -Filter "Microsoft.VCLibs.x64*.appx" -ErrorAction SilentlyContinue | Select-Object -First 1
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
Copy-Item (Join-Path $root "Pulse\Assets\app.ico") (Join-Path $stage "app.ico")
Copy-Item (Join-Path $depDir "dep.runtime.msix") (Join-Path $stage "dep.runtime.msix")
Copy-Item (Join-Path $depDir "dep.vclibs.appx") (Join-Path $stage "dep.vclibs.appx")

# ---- build the graphical installer (WPF, .NET Framework 4.8) ----
# msix, cert and framework dependencies are embedded inside the single exe.
# Same command line as the old console installer: /s /silent /quiet  (winget relies on this).
$setupProj = Join-Path $root "src\Pulse.Setup\Pulse.Setup.csproj"
$setupExe = Join-Path $outDir "Pulse-$installerVer-Setup.exe"
Remove-Item $setupExe -Force -ErrorAction SilentlyContinue
$buildOut = Join-Path $env:TEMP "TMP-setup-out"
Remove-Item $buildOut -Recurse -Force -ErrorAction SilentlyContinue

# 1) small payload-free build = the uninstaller (embedded into the setup, registered in Programs and Features)
$uninstOut = Join-Path $env:TEMP "TMP-uninstall-out"
Remove-Item $uninstOut -Recurse -Force -ErrorAction SilentlyContinue
dotnet build $setupProj -c Release -p:PulseVersion=$installerVer.0 -o $uninstOut -v:q -nologo --no-incremental
if ($LASTEXITCODE -ne 0) { Write-Host "uninstaller build failed" -ForegroundColor Red; exit 1 }
Copy-Item (Join-Path $uninstOut "Pulse-Setup.exe") (Join-Path $stage "uninstall.exe")

# 2) the setup itself, with every payload embedded
dotnet build $setupProj -c Release -p:PayloadDir="$stage" -p:PulseVersion=$installerVer.0 `
    -o $buildOut -v:q -nologo --no-incremental
if ($LASTEXITCODE -ne 0) { Write-Host "setup build failed" -ForegroundColor Red; exit 1 }
Copy-Item (Join-Path $buildOut "Pulse-Setup.exe") $setupExe -Force
try {
    Set-AuthenticodeSignature -FilePath $setupExe -Certificate $cert | Out-Null
} catch { }

if (Test-Path $setupExe) {
    $mb = [Math]::Round((Get-Item $setupExe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "DONE!  $setupExe  ($mb MB)" -ForegroundColor Green
    Write-Host "Share this single file - users just run it." -ForegroundColor Green
} else {
    Write-Host "Failed to produce the exe." -ForegroundColor Red
    exit 1
}
