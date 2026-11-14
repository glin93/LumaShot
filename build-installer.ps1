param([string]$Compiler = 'ISCC.exe', [string]$AppSource = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath (Join-Path $AppSource 'LumaShot.exe'))) { throw 'Run bootstrap-windows.ps1 or build.ps1 first.' }
& $Compiler /Q ('/DAppSource='+[IO.Path]::GetFullPath($AppSource)) (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
