$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$output = Join-Path $taskRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$iconPath = Join-Path $PSScriptRoot 'assets/teck.ico'
if (!(Test-Path -LiteralPath $iconPath)) { throw 'Missing app icon: assets/teck.ico' }
$exe = Join-Path $output 'Teck-PS1-VI-EN.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$compileArgs = @('/nologo','/target:winexe','/main:TeckPS1','/platform:anycpu','/optimize+','/utf8output',('/out:'+$exe),('/win32icon:'+$iconPath),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll')
foreach ($pkg in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'localized-packages') -Filter '*.zip') { $compileArgs += ('/resource:'+$pkg.FullName+',pkg.'+$pkg.Name) }
$compileArgs += '/resource:'+(Join-Path $PSScriptRoot 'assets/teck-background.png')+',asset.background'
$compileArgs += '/resource:'+(Join-Path $PSScriptRoot 'assets/VT323-Regular.ttf')+',asset.titlefont'
$compileArgs += '/resource:'+(Join-Path $PSScriptRoot 'assets/VT323-OFL.txt')+',asset.fontlicense'
$compileArgs += '/resource:'+$iconPath+',asset.icon'
$compileArgs += '/resource:'+(Join-Path $PSScriptRoot 'assets/ps1-photo-base.png')+',asset.ps1base'
$compileArgs += '/resource:'+(Join-Path $PSScriptRoot 'assets/ps1-photo-lid.png')+',asset.ps1lid'
$compileArgs += '/resource:'+(Join-Path $PSScriptRoot 'assets/ps1-photo-closed.png')+',asset.ps1closed'
$compileArgs += Join-Path $PSScriptRoot 'src/TeckPS1Launcher.cs'
& $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
$testFolder = Join-Path $output ('teck-test-'+[guid]::NewGuid().ToString('N'))
$process = Start-Process -FilePath $exe -ArgumentList @('--self-test',('"'+$testFolder+'"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw (Get-Content -LiteralPath (Join-Path $testFolder 'SELFTEST-FAILED.txt') -Raw) }
Get-Content -LiteralPath (Join-Path $testFolder 'SELFTEST-PASSED.txt')
$previewPath = Join-Path $output 'Teck-PS1-VI-EN-Preview.png'
$process = Start-Process -FilePath $exe -ArgumentList @('--preview',('"'+$previewPath+'"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw 'UI preview failed' }
Get-FileHash -LiteralPath $exe -Algorithm SHA256
