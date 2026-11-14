$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$framework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$output=Join-Path $PSScriptRoot 'images'
$data=Join-Path ([IO.Path]::GetTempPath()) ('LumaShot-showcase-'+[Guid]::NewGuid().ToString('N'))
$exe=Join-Path $root 'LumaShot.Showcase.exe'
$references=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Xml.dll','WPF\WindowsBase.dll','System.Xaml.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$compilerArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/main:LumaShot.ShowcaseProgram',('/out:'+$exe),('/win32icon:'+(Join-Path $root 'LumaShot.ico')))
foreach($reference in $references){$compilerArgs+='/reference:'+(Join-Path $framework $reference)}
$compilerArgs+=(Get-ChildItem -LiteralPath $root -Filter '*.cs').FullName
$compilerArgs+=@("/reference:$root\MdXaml.dll","/reference:$root\MdXaml.Plugins.dll","/reference:$root\ICSharpCode.AvalonEdit.dll")
$compilerArgs+=Join-Path $PSScriptRoot 'render-showcase.cs'
try {
 & (Join-Path $framework 'csc.exe') @compilerArgs
 if($LASTEXITCODE -ne 0){throw 'Showcase renderer build failed'}
 $process=Start-Process -FilePath $exe -ArgumentList @(('"'+$data+'"'),('"'+$output+'"')) -WindowStyle Hidden -PassThru -Wait
 if($process.ExitCode -ne 0){throw (Get-Content (Join-Path $data 'showcase-error.txt') -Raw)}
 Get-ChildItem -LiteralPath $output -Filter '*.png' | Select-Object Name,Length
} finally {
 if(Test-Path -LiteralPath $exe){Remove-Item -LiteralPath $exe -Force}
}
