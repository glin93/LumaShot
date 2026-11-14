$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$cache=Join-Path $root '.build-cache'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$package=Join-Path $cache 'dotnet-apphost-10.0.14.nupkg'
if(!(Test-Path -LiteralPath $package)){
 Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.host.win-x64/10.0.14/microsoft.netcore.app.host.win-x64.10.0.14.nupkg' -OutFile $package
}
if((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne '2A4547B0E6CF0DD15E723531DAA5450D99D4E151105AEC0D31F2B0C7CFB286DB'){throw 'Apphost checksum mismatch'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($package)
try{
 $entry=$zip.GetEntry('runtimes/win-x64/native/apphost.exe')
 $stream=$entry.Open();$buffer=New-Object IO.MemoryStream
 try{$stream.CopyTo($buffer);$bytes=$buffer.ToArray()}finally{$stream.Dispose();$buffer.Dispose()}
}finally{$zip.Dispose()}
# The official .NET apphost template reserves 1024 bytes for the managed entry
# point. Fill that slot, leaving the upstream ShareX launcher unmodified.
$placeholder='c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2'
$offset=[Text.Encoding]::ASCII.GetString($bytes).IndexOf($placeholder,[StringComparison]::Ordinal)
if($offset -lt 0){throw 'Apphost entry-point placeholder missing'}
[Array]::Clear($bytes,$offset,1024)
$name=[Text.Encoding]::UTF8.GetBytes('LumaShot.Capture.dll');[Array]::Copy($name,0,$bytes,$offset,$name.Length)
$engine=Join-Path $root 'engine'
[IO.File]::WriteAllBytes((Join-Path $engine 'LumaShot.Capture.exe'),$bytes)
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /optimize+ /utf8output "/out:$engine\LumaShot.Capture.dll" "$root\capture-host\CaptureHost.cs"
if($LASTEXITCODE -ne 0){throw 'Capture host build failed'}
$runtimeConfig=Get-Content -LiteralPath (Join-Path $engine 'ShareX.runtimeconfig.json') -Raw | ConvertFrom-Json
$runtimeConfig.runtimeOptions.configProperties | Add-Member -NotePropertyName 'System.GC.ConserveMemory' -NotePropertyValue 6 -Force
$runtimeConfig | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $engine 'LumaShot.Capture.runtimeconfig.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $engine 'ShareX.deps.json') -Destination (Join-Path $engine 'LumaShot.Capture.deps.json') -Force
Write-Output 'Built reusable capture host'
