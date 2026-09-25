param([Parameter(Mandatory=$true)][string]$Publish,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$publishPath=(Resolve-Path -LiteralPath $Publish).Path
$outputPath=[IO.Path]::GetFullPath($Output)
if(Test-Path -LiteralPath $outputPath){throw 'Use a new, empty package output directory'}
New-Item -ItemType Directory -Path (Join-Path $outputPath 'bin') -Force | Out-Null
foreach($name in @('DragonsDogma2DualSense.dll','DragonsDogma2DualSense.deps.json','DragonsDogma2DualSense.runtimeconfig.json')){
 Copy-Item -LiteralPath (Join-Path $publishPath $name) -Destination (Join-Path $outputPath 'bin')
}
foreach($name in @('libportaudio64bit.dll','catalog.json','dd2_output_observer.dll')){Copy-Item -LiteralPath (Join-Path $root "distribution/$name") -Destination (Join-Path $outputPath 'bin')}
Copy-Item -LiteralPath (Join-Path $root 'reframework/autorun/dd2_dualsense_bridge.lua') -Destination (Join-Path $outputPath 'bin')
foreach($name in @('Setup.cmd','Start-Mod.cmd','Uninstall.cmd','config.json','THIRD_PARTY_NOTICES.txt','MinHook-LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $root "distribution/$name") -Destination $outputPath}
Copy-Item -LiteralPath (Join-Path $root 'README.md'),(Join-Path $root 'LICENSE') -Destination $outputPath
Write-Output "Package assembled: $outputPath"
