$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path $compiler)) { throw 'Install .NET Framework 4.8 on Windows, then try again.' }
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $destination | Out-Null

# Branding: branding\logo.png (your own artwork) wins over the vector rendition in branding\logo-vector.png.
$logo = Join-Path $PSScriptRoot 'branding\logo.png'
if (-not (Test-Path $logo)) { $logo = Join-Path $PSScriptRoot 'branding\logo-vector.png' }
$icon = Join-Path $destination 'WinDotra.ico'
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile($logo)
try {
  # A multi-size .ico with PNG-compressed entries (Windows Vista and later read these).
  $sizes = 16, 24, 32, 48, 64, 128, 256; $images = @()
  foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap); $graphics.InterpolationMode = 'HighQualityBicubic'; $graphics.SmoothingMode = 'HighQuality'; $graphics.PixelOffsetMode = 'HighQuality'
    $graphics.DrawImage($source, 0, 0, $size, $size); $graphics.Dispose()
    $stream = New-Object System.IO.MemoryStream; $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png); $bitmap.Dispose(); $images += ,$stream.ToArray()
  }
  $file = New-Object System.IO.BinaryWriter([System.IO.File]::Create($icon))
  $file.Write([uint16]0); $file.Write([uint16]1); $file.Write([uint16]$sizes.Count); $offset = 6 + 16 * $sizes.Count
  for ($i = 0; $i -lt $sizes.Count; $i++) { $s = $sizes[$i]; $file.Write([byte]($s % 256)); $file.Write([byte]($s % 256)); $file.Write([byte]0); $file.Write([byte]0); $file.Write([uint16]1); $file.Write([uint16]32); $file.Write([uint32]$images[$i].Length); $file.Write([uint32]$offset); $offset += $images[$i].Length }
  foreach ($image in $images) { $file.Write($image) }
  $file.Close()
} finally { $source.Dispose() }

$references = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Security.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.VisualBasic.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',"/out:$destination\WinDotra.exe","/win32icon:$icon","/resource:$PSScriptRoot\src\MainWindow.xaml,MainWindow.xaml","/resource:$logo,logo.png")
foreach ($reference in $references) { $arguments += "/reference:$(Join-Path $framework $reference)" }
# Facades and Windows metadata for the island's now-playing media (Windows 10 1809+). All ship with Windows.
foreach ($reference in @('System.Runtime.dll','System.Threading.Tasks.dll')) { $arguments += "/reference:$(Join-Path $framework $reference)" }
foreach ($winmd in @('Windows.Foundation.winmd','Windows.Media.winmd','Windows.Storage.winmd')) { $arguments += "/reference:$(Join-Path $env:WINDIR "System32\WinMetadata\$winmd")" }
$arguments += @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item (Join-Path $PSScriptRoot 'src\App.config') (Join-Path $destination 'WinDotra.exe.config') -Force
# Inter (SIL Open Font License) ships next to the app as the default UI font.
New-Item -ItemType Directory -Force -Path (Join-Path $destination 'fonts') | Out-Null; Copy-Item (Join-Path $PSScriptRoot 'fonts\*') (Join-Path $destination 'fonts') -Force
# Community styles (.windotra-style files) ship next to the app and show up in the Styles gallery.
if (Test-Path (Join-Path $PSScriptRoot 'styles')) { New-Item -ItemType Directory -Force -Path (Join-Path $destination 'styles') | Out-Null; Copy-Item (Join-Path $PSScriptRoot 'styles\*') (Join-Path $destination 'styles') -Force }
# Public-domain cursor sets used by styles (cursors\<name>\*.cur).
if (Test-Path (Join-Path $PSScriptRoot 'cursors')) { Copy-Item (Join-Path $PSScriptRoot 'cursors') $destination -Recurse -Force }
Write-Host "Built $destination\WinDotra.exe"

# The community store's catalog ships inside WinDotra so the Store page works offline.
if (Test-Path (Join-Path $PSScriptRoot 'docs\store')) { Copy-Item (Join-Path $PSScriptRoot 'docs\store') $destination -Recurse -Force }
