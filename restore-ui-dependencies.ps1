$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$cache=Join-Path $root '.build-cache/ui'
$packages=@(
 @{Id='mdxaml';Version='1.27.0';Dll='MdXaml.dll';Hash='A3927AE3CD24BDCEA19AB9EE7EA63FF205044C7DD8A4E2F007E6A44FE61D436E'},
 @{Id='mdxaml.plugins';Version='1.27.0';Dll='MdXaml.Plugins.dll';Hash='962A4626F57D97A063E1DC9E61B2B42B0DCDABEB121ADB3285015495CB7B9658'},
 @{Id='avalonedit';Version='6.3.0.90';Dll='ICSharpCode.AvalonEdit.dll';Hash='DE10BDA61CA4EB20EE29F3C5BCEEDF9E8573F1871DCF437A67D8506EA60E95CF'}
)
New-Item -ItemType Directory -Path $cache -Force | Out-Null
foreach($package in $packages){
 $zip=Join-Path $cache ($package.Id+'.zip')
 if(!(Test-Path -LiteralPath $zip)){Invoke-WebRequest -Uri ('https://api.nuget.org/v3-flatcontainer/'+$package.Id+'/'+$package.Version+'/'+$package.Id+'.'+$package.Version+'.nupkg') -OutFile $zip}
 if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $package.Hash){throw ('Checksum mismatch: '+$package.Id)}
 $expanded=Join-Path $cache $package.Id
 if(!(Test-Path (Join-Path $expanded 'lib/net462'))){Expand-Archive -LiteralPath $zip -DestinationPath $expanded -Force}
 $source=Join-Path $expanded ('lib/net462/'+$package.Dll)
 $target=Join-Path $root $package.Dll
 if(!(Test-Path $target) -or (Get-FileHash $source).Hash -ne (Get-FileHash $target).Hash){Copy-Item -LiteralPath $source -Destination $target -Force}
}
