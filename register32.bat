@setlocal enableextensions
@cd /d "%~dp0"

"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /register /codebase "./NotakeyNETProvider/bin/Debug/NotakeyNETProvider.dll"
reg import register.reg /reg:32
NotakeyBGService\winsw.exe install
NotakeyBGService\winsw.exe start

pause
