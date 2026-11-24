$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

Write-Host "1. Building Search.exe (Native x64 + WebView2 + embedded icon)..."
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:"$dir\search.ico" /out:"$dir\Search.exe" `
    /r:"$wpf\PresentationFramework.dll" `
    /r:"$wpf\PresentationCore.dll" `
    /r:"$wpf\WindowsBase.dll" `
    /r:"System.Xaml.dll" `
    /r:"System.Runtime.Serialization.dll" `
    /r:"System.Security.dll" `
    /r:"System.Drawing.dll" `
    /r:"System.Windows.Forms.dll" `
    /r:"System.IO.Compression.dll" `
    /r:"System.IO.Compression.FileSystem.dll" `
    /r:"System.Net.Http.dll" `
    /r:"Microsoft.CSharp.dll" `
    /r:"$dir\Microsoft.Web.WebView2.Core.dll" `
    /r:"$dir\Microsoft.Web.WebView2.Wpf.dll" `
    /r:"$dir\Microsoft.Web.WebView2.WinForms.dll" `
    "$dir\src\*.cs"

if ($LASTEXITCODE -ne 0) { throw "Search.exe compilation failed" }

Write-Host "2. Packaging payload.zip for standalone installer..."
$payloadZip = "$dir\payload.zip"
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
$filesToPack = @(
    "$dir\Search.exe",
    "$dir\search.ico",
    "$dir\Microsoft.Web.WebView2.Core.dll",
    "$dir\Microsoft.Web.WebView2.Wpf.dll",
    "$dir\Microsoft.Web.WebView2.WinForms.dll",
    "$dir\WebView2Loader.dll"
)
Compress-Archive -Path $filesToPack -DestinationPath $payloadZip -Force

Write-Host "3. Compiling standalone Search-Setup.exe installer..."
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:"$dir\search.ico" /out:"$dir\Search-Setup.exe" `
    /resource:"$payloadZip,payload.zip" `
    /r:"$wpf\PresentationFramework.dll" `
    /r:"$wpf\PresentationCore.dll" `
    /r:"$wpf\WindowsBase.dll" `
    /r:"System.Xaml.dll" `
    /r:"System.IO.Compression.dll" `
    /r:"System.IO.Compression.FileSystem.dll" `
    /r:"Microsoft.CSharp.dll" `
    "$dir\InstallerSetup.cs"

if ($LASTEXITCODE -ne 0) { throw "Search-Setup.exe compilation failed" }
Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue

Write-Host "Done! Built Search.exe and Search-Setup.exe"
