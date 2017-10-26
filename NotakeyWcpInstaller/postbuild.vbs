set o_installer = CreateObject("WindowsInstaller.Installer")
set o_database = o_Installer.OpenDatabase(WScript.Arguments(0) & "Release/NotakeyWcpInstaller.msi", 1)
s_SQL = "INSERT INTO Property (Property, Value) Values( 'IS_PS_EXECUTIONPOLICY', 'Bypass')"
Set o_MSIView = o_DataBase.OpenView( s_SQL)
o_MSIView.Execute
o_DataBase.Commit
