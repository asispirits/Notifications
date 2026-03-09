@echo off
setlocal EnableExtensions

if "%~7"=="" goto :usage

set "CONFIG_PATH=%~1"
set "USER_ID=%~2"
set "TENANT=%~3"
set "BRAND=%~4"
set "REGION=%~5"
set "SITE=%~6"
set "ROLE=%~7"
set "ENVIRONMENT=%~8"
set "EXTRA_SEGMENTS=%~9"

if "%ENVIRONMENT%"=="" set "ENVIRONMENT=prod"

set "SCRIPT_DIR=%~dp0"
set "GEN_PS=%SCRIPT_DIR%New-SegmentFilters.ps1"
set "SET_PS=%SCRIPT_DIR%Set-NotificationsEndpointConfig.ps1"

if not exist "%GEN_PS%" (
  echo ERROR: Missing "%GEN_PS%"
  exit /b 1
)

if not exist "%SET_PS%" (
  echo ERROR: Missing "%SET_PS%"
  exit /b 1
)

where powershell.exe >nul 2>nul
if errorlevel 1 (
  echo ERROR: powershell.exe was not found on this system.
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "^$ErrorActionPreference='Stop'; ^$gen = & ^$env:GEN_PS -UserId ^$env:USER_ID -Tenant ^$env:TENANT -Brand ^$env:BRAND -Region ^$env:REGION -Site ^$env:SITE -Role ^$env:ROLE -Environment ^$env:ENVIRONMENT -ExtraSegments ^$env:EXTRA_SEGMENTS ^| ConvertFrom-Json; & ^$env:SET_PS -ConfigPath ^$env:CONFIG_PATH -UserId ^$gen.UserId -SegmentFilters ^$gen.SegmentFilters -IncludeViewerUserIdSegment:^$false -SegmentMultiUser:^$true; Write-Host ('Completed. SegmentFilters: ' + ^$gen.SegmentFilters)"
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" exit /b %EXIT_CODE%

exit /b 0

:usage
echo Usage:
echo   Configure-EndpointSegmentation.bat ^<ConfigPath^> ^<UserId^> ^<Tenant^> ^<Brand^> ^<Region^> ^<Site^> ^<Role^> [Environment] [ExtraSegments]
echo.
echo Example:
echo   Configure-EndpointSegmentation.bat "C:\Program Files\Notifications\Notifications.config.json" "store-104-pos-01" "asi" "spirits" "east" "store-104" "cashier" "prod"
exit /b 1
