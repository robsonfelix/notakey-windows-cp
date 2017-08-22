@setlocal enableextensions
@cd /d "%~dp0"

reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" /v ExcludedCredentialProviders /reg:64

pause
