NotakeyBGService\winsw.exe uninstall
NotakeyBGService\winsw.exe stop
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /unregister /codebase "./NotakeyNETProvider/bin/x64/Debug/NotakeyNETProvider.dll"
reg delete "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{77E5F42E-B280-4219-B130-D48BB3932A04}"
