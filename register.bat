"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /register /codebase "./NotakeyNETProvider/bin/x64/Debug/NotakeyNETProvider.dll"
register.reg
NotakeyBGService\winsw.exe install
NotakeyBGService\winsw.exe start
