set o_installer = CreateObject("WindowsInstaller.Installer")
o_path = WScript.Arguments(0)

' MsgBox  o_path & "Release\NotakeyWcpInstaller.msi"
oFile = o_path & "Release\NotakeyWcpInstaller.msi"

set o_database = o_Installer.OpenDatabase(oFile, 1)

' s_SQL = "INSERT INTO Property (Property, Value) Values( 'IS_PS_EXECUTIONPOLICY', 'Bypass')"
' set o_MSIView = o_DataBase.OpenView( s_SQL)
' o_MSIView.Execute

set FSO = CreateObject("Scripting.FileSystemObject")
set objFileToRead = FSO.OpenTextFile(o_path & "..\VERSION",1)
strVersion = Trim(objFileToRead.ReadAll())
objFileToRead.Close
set objFileToRead = Nothing

' s_SQL = "UPDATE Property SET Value='" & strVersion & "' WHERE Property = 'ProductVersion'"
' set o_MSIView = o_DataBase.OpenView( s_SQL)
' o_MSIView.Execute

s_SQL = "UPDATE Registry SET Value='" & strVersion & "' WHERE Name = 'ProductVersion' AND Value = '{VERSION}'"
set o_MSIView = o_DataBase.OpenView( s_SQL)
o_MSIView.Execute

' s_SQL = "UPDATE `Upgrade` SET `VersionMax` = '" & strVersion & "' WHERE `ActionProperty` = 'PREVIOUSVERSIONSINSTALLED'"
' set o_MSIView = o_DataBase.OpenView( s_SQL)
' o_MSIView.Execute

' s_SQL = "UPDATE `Upgrade` SET VersionMin = '" & strVersion & "' WHERE ActionProperty = 'NEWERPRODUCTFOUND'"
' set o_MSIView = o_DataBase.OpenView( s_SQL)
' o_MSIView.Execute

o_DataBase.Commit

set o_MSIView = Nothing
set o_DataBase = Nothing

oDestFile = o_path & "Release\NotakeyWcpInstaller-" & strVersion & ".msi"

' MsgBox  oFile & " move to " & oDestFile

FSO.MoveFile oFile, oDestFile
