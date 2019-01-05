[CmdletBinding()]
param
(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [int] $APPCENTER_BUILD_ID = $($env:APPCENTER_BUILD_ID -as [int])
)

$AndroidManifestPath = Join-Path -Path $PSScriptRoot -ChildPath 'wzxv.droid\Properties\AndroidManifest.xml'
[xml]$AndroidManifest = Get-Content -Path $AndroidManifestPath -ErrorAction Stop
$AndroidManifest.manifest.versionName += ".$($APPCENTER_BUILD_ID)"
$AndroidManifest.Save($AndroidManifestPath)