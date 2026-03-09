param(
  [Parameter(Mandatory = $true)]
  [string]$ConfigPath,

  [Parameter(Mandatory = $true)]
  [string]$UserId,

  [Parameter(Mandatory = $true)]
  [string]$SegmentFilters,

  [bool]$IncludeViewerUserIdSegment = $false,
  [bool]$SegmentMultiUser = $true,
  [string]$SegmentForceFilter = ""
)

function Coalesce-String {
  param([AllowNull()][string]$Value)
  if ($null -eq $Value) { return "" }
  return $Value
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
  throw "Config file not found: $ConfigPath"
}

$json = Get-Content -LiteralPath $ConfigPath -Raw
if ([string]::IsNullOrWhiteSpace($json)) {
  throw "Config file is empty: $ConfigPath"
}

$config = $json | ConvertFrom-Json
if ($null -eq $config) {
  throw "Unable to parse config JSON: $ConfigPath"
}

$config.UserId = (Coalesce-String $UserId).Trim()
$config.SegmentFilters = (Coalesce-String $SegmentFilters).Trim()
$config.IncludeViewerUserIdSegment = $IncludeViewerUserIdSegment
$config.SegmentMultiUser = $SegmentMultiUser
$config.SegmentForceFilter = (Coalesce-String $SegmentForceFilter).Trim()

if ($null -eq $config.PSObject.Properties['SegmentRole']) {
  $config | Add-Member -NotePropertyName SegmentRole -NotePropertyValue ""
} else {
  $config.SegmentRole = ""
}

if ($null -eq $config.PSObject.Properties['SegmentFilter']) {
  $config | Add-Member -NotePropertyName SegmentFilter -NotePropertyValue ""
} else {
  $config.SegmentFilter = ""
}

$config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $ConfigPath -Encoding utf8

Write-Host "Updated config: $ConfigPath"
Write-Host "UserId: $($config.UserId)"
Write-Host "SegmentFilters: $($config.SegmentFilters)"
Write-Host "IncludeViewerUserIdSegment: $($config.IncludeViewerUserIdSegment)"
Write-Host "SegmentMultiUser: $($config.SegmentMultiUser)"
Write-Host "SegmentForceFilter: $($config.SegmentForceFilter)"
