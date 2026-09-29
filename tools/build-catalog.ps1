param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$catalogRoot=Join-Path (Split-Path $PSScriptRoot -Parent) 'distribution/catalog'
$manifest=Get-Content -LiteralPath (Join-Path $catalogRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$catalog=[ordered]@{format=$manifest.format}
foreach($section in $manifest.sections.PSObject.Properties){
 $entries=[ordered]@{}
 foreach($file in $section.Value){
  $part=Get-Content -LiteralPath (Join-Path $catalogRoot $file) -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach($entry in $part.PSObject.Properties){
   if($entries.Contains($entry.Name)){throw "Duplicate $($section.Name) entry: $($entry.Name)"}
   $entries.Add($entry.Name,$entry.Value)
  }
 }
 $catalog.Add($section.Name,$entries)
}
$outputPath=[IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath)) | Out-Null
[IO.File]::WriteAllText($outputPath,($catalog | ConvertTo-Json -Depth 100 -Compress),[Text.UTF8Encoding]::new($false))
Write-Output "Catalog assembled: $outputPath"
