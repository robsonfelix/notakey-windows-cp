@setlocal enableextensions
@cd /d "%~dp0"

"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /register /codebase "./NotakeyNETProvider/bin/x64/Debug/NotakeyNETProvider.dll"
reg import register.reg /reg:64
NotakeyBGService\winsw.exe install
NotakeyBGService\winsw.exe start

pause
