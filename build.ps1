param([string]$OutputName='LumaShot.exe')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'restore-ui-dependencies.ps1')
& (Join-Path $root 'build-icon.ps1')
& (Join-Path $root 'build-capture-host.ps1')
[xml]$layoutCheck = [IO.File]::ReadAllText((Join-Path $root 'Main.xaml'))
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Xml.dll','WPF\WindowsBase.dll','System.Xaml.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output',"/out:$root\$OutputName","/win32manifest:$root\app.manifest","/win32icon:$root\LumaShot.ico")
foreach ($reference in $references) { $compilerArgs += "/reference:$framework\$reference" }
$compilerArgs += @("/reference:$root\MdXaml.dll","/reference:$root\MdXaml.Plugins.dll","/reference:$root\ICSharpCode.AvalonEdit.dll")
$compilerArgs += (Get-ChildItem -LiteralPath $root -Filter '*.cs').FullName
& "$framework\csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output 'Built LumaShot.exe'
& "$framework\csc.exe" /nologo /target:exe /platform:x64 /optimize+ "/reference:$framework\System.Security.dll" "/out:$root\backend\SecureStore.exe" "$root\backend\SecureStore.cs"
if ($LASTEXITCODE -ne 0) { throw 'Credential helper build failed' }
& "$root\runtime\node.exe" "$root\backend\build.mjs"
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed' }
