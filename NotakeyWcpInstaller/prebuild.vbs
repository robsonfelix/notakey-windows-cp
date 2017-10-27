Const ForReading = 1

o_path = WScript.Arguments(0)

set FSO = CreateObject("Scripting.FileSystemObject")
set objFileToRead = FSO.OpenTextFile(o_path & "..\VERSION",1)
strVersion = Replace(Trim(objFileToRead.ReadAll()), vbCrLf, "")
objFileToRead.Close
set objFileToRead = Nothing

' "ProductVersion" = "8:1.0.0"
' 8:{VERSION}

Set objFile = FSO.OpenTextFile(o_path & "\NotakeyWcpInstaller.vdproj.template", ForReading)

strText = objFile.ReadAll

objFile.Close

strNewText = Replace(strText, "8:{VERSION}", "8:" & strVersion)
strNewText = Replace(strNewText, "8:1.0.0", "8:" & strVersion)

Set objFile = FSO.CreateTextFile(o_path & "\NotakeyWcpInstaller.vdproj", True, False)

objFile.WriteLine strNewText

objFile.Close
