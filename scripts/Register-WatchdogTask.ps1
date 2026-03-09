param(
  [Parameter(Mandatory=$false)]
  [string]$ExePath = "$PSScriptRoot\..\bin\Release\net8.0-windows\win-x64\publish\Notifications.exe",

  [Parameter(Mandatory=$false)]
  [string]$TaskName = "Notifications Widget Watchdog",

  [Parameter(Mandatory=$false)]
  [int]$IntervalMinutes = 30
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($id)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script as Administrator."
  }
}

Assert-Admin

$resolvedExe = [System.IO.Path]::GetFullPath($ExePath)
if (-not (Test-Path $resolvedExe)) {
  throw "Notifications.exe not found at: $resolvedExe"
}

if ($IntervalMinutes -lt 5) { $IntervalMinutes = 5 }
if ($IntervalMinutes -gt 240) { $IntervalMinutes = 240 }

$procName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedExe)
$escapedExe = $resolvedExe.Replace("'", "''")
$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name

$action = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command `"if (-not (Get-Process -Name '$procName' -ErrorAction SilentlyContinue)) { Start-Process -FilePath '$escapedExe' }`""

$argList = @(
  "/Create",
  "/F",
  "/TN", $TaskName,
  "/TR", $action,
  "/SC", "MINUTE",
  "/MO", "$IntervalMinutes",
  "/RL", "HIGHEST",
  "/RU", $currentUser
)

$create = Start-Process -FilePath "schtasks.exe" -ArgumentList $argList -Wait -NoNewWindow -PassThru
if ($create.ExitCode -ne 0) {
  throw "schtasks /Create failed with exit code $($create.ExitCode)."
}

$verify = Start-Process -FilePath "schtasks.exe" -ArgumentList @("/Query", "/TN", $TaskName, "/FO", "LIST") -Wait -NoNewWindow -PassThru
if ($verify.ExitCode -ne 0) {
  throw "Task created but query failed with exit code $($verify.ExitCode)."
}

Write-Host "Watchdog task registered successfully."
Write-Host "Task Name: $TaskName"
Write-Host "Interval (minutes): $IntervalMinutes"
Write-Host "Executable: $resolvedExe"
