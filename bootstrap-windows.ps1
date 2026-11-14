$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$cache = Join-Path $root '.build-cache'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$packages = @(
    @{ Name='sharex'; Url='https://github.com/ShareX/ShareX/releases/download/v22.4.0/ShareX-22.4.0-portable-x64.zip'; Hash='C321ED353A6C6AE32093927F97C22E0F0627628CE6FA47ED366BCEFD05936C7B' },
    @{ Name='node'; Url='https://nodejs.org/dist/v24.22.3/node-v24.22.3-win-x64.zip'; Hash='E612AF99F626AB415EA225AF6BCE5C971FEB448CB5CAE250BB1C3FA50C5F7D9F' },
    @{ Name='rclone'; Url='https://downloads.rclone.org/v1.76.0/rclone-v1.76.0-windows-amd64.zip'; Hash='8B4FD2737ADE4E70C057EF4A49DEB7CFFE94B0FB9D7587C189D149823F21A1DC' }
)
foreach ($package in $packages) {
    $zip = Join-Path $cache ($package.Name+'.zip')
    if (!(Test-Path -LiteralPath $zip)) { Invoke-WebRequest -Uri $package.Url -OutFile $zip }
    if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $package.Hash) { throw ('Checksum mismatch: '+$package.Name) }
    $dest = Join-Path $cache $package.Name
    if (!(Test-Path -LiteralPath $dest)) { Expand-Archive -LiteralPath $zip -DestinationPath $dest }
}
$engine = Join-Path $root 'engine'
if (!(Test-Path -LiteralPath (Join-Path $engine 'ShareX.exe'))) {
    New-Item -ItemType Directory -Path $engine -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $cache 'sharex') | Copy-Item -Destination $engine -Recurse -Force
}
$nodeRoot = Join-Path $cache 'node/node-v24.22.3-win-x64'
$runtime = Join-Path $root 'runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $nodeRoot 'node.exe') -Destination $runtime -Force
Copy-Item -LiteralPath (Join-Path $nodeRoot 'LICENSE') -Destination (Join-Path $runtime 'LICENSE.txt') -Force
$syncTools = Join-Path $root 'sync-tools'
New-Item -ItemType Directory -Path $syncTools -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $cache 'rclone/rclone-v1.76.0-windows-amd64') -File | Copy-Item -Destination $syncTools -Force
Push-Location (Join-Path $root 'backend')
try {
    & (Join-Path $runtime 'node.exe') (Join-Path $nodeRoot 'node_modules/npm/bin/npm-cli.js') ci --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
} finally { Pop-Location }
& (Join-Path $root 'build.ps1')
