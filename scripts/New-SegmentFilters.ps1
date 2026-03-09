param(
  [Parameter(Mandatory = $true)]
  [string]$UserId,

  [string]$Tenant,
  [string]$Brand,
  [string]$Region,
  [string]$Market,
  [string]$Site,
  [string]$Role,
  [string]$Environment = "prod",
  [string]$ExtraSegments = "",
  [switch]$IncludeDeviceSegment,
  [switch]$IncludeUserSegment
)

function Coalesce-String {
  param([AllowNull()][string]$Value)
  if ($null -eq $Value) { return "" }
  return $Value
}

function Normalize-Value {
  param([AllowNull()][string]$Value)

  $v = (Coalesce-String $Value).Trim().ToLowerInvariant()
  if ([string]::IsNullOrWhiteSpace($v)) { return "" }

  $v = $v -replace "\s+", "-"
  $v = $v -replace "[^a-z0-9\-_]", ""
  return $v
}

function Add-Segment {
  param(
    [System.Collections.Generic.HashSet[string]]$Seen,
    [System.Collections.Generic.List[string]]$Segments,
    [AllowNull()][string]$Segment
  )

  $s = (Coalesce-String $Segment).Trim()
  if ([string]::IsNullOrWhiteSpace($s)) { return }
  if ($Seen.Add($s)) {
    $Segments.Add($s) | Out-Null
  }
}

$seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$segments = [System.Collections.Generic.List[string]]::new()

$normalizedUserId = Normalize-Value $UserId
if ([string]::IsNullOrWhiteSpace($normalizedUserId)) {
  throw "UserId produced an empty normalized value. Provide a stable endpoint id (example: store-104-pos-01)."
}

$parts = @(
  @{ Key = "tenant"; Value = $Tenant },
  @{ Key = "brand"; Value = $Brand },
  @{ Key = "region"; Value = $Region },
  @{ Key = "market"; Value = $Market },
  @{ Key = "site"; Value = $Site },
  @{ Key = "role"; Value = $Role },
  @{ Key = "env"; Value = $Environment }
)

foreach ($part in $parts) {
  $value = Normalize-Value $part.Value
  if ([string]::IsNullOrWhiteSpace($value)) { continue }
  Add-Segment -Seen $seen -Segments $segments -Segment ("{0}:{1}" -f $part.Key, $value)
}

if ($IncludeDeviceSegment.IsPresent) {
  Add-Segment -Seen $seen -Segments $segments -Segment ("device:{0}" -f $normalizedUserId)
}

if ($IncludeUserSegment.IsPresent) {
  Add-Segment -Seen $seen -Segments $segments -Segment ("user:{0}" -f $normalizedUserId)
}

if (-not [string]::IsNullOrWhiteSpace($ExtraSegments)) {
  $tokens = $ExtraSegments -split '[;,]'
  foreach ($token in $tokens) {
    $raw = (Coalesce-String $token).Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($raw)) { continue }

    $normalized = $raw -replace "\s+", "-"
    $normalized = $normalized -replace "[^a-z0-9\-_:]", ""
    Add-Segment -Seen $seen -Segments $segments -Segment $normalized
  }
}

$result = [pscustomobject]@{
  UserId = $normalizedUserId
  SegmentFilters = ($segments -join ';')
}

$result | ConvertTo-Json -Depth 4
