Option Explicit
Dim mode, dllPath, sw, activeDoc, result, fso
If WScript.Arguments.Count <> 2 Then
    WScript.Echo "Usage: lab-addin-control.vbs <probe|load|unload|exit> <dll-path>"
    WScript.Quit 64
End If
mode = LCase(WScript.Arguments(0))
dllPath = WScript.Arguments(1)
If mode <> "probe" And mode <> "load" And mode <> "unload" And mode <> "exit" Then
    WScript.Echo "Unsupported mode: " & mode
    WScript.Quit 64
End If
Set fso = CreateObject("Scripting.FileSystemObject")
If mode <> "probe" And mode <> "exit" And Not fso.FileExists(dllPath) Then
    WScript.Echo "Lab DLL not found: " & dllPath
    WScript.Quit 66
End If
On Error Resume Next
Set sw = GetObject(, "SldWorks.Application")
If Err.Number <> 0 Or sw Is Nothing Then
    WScript.Echo "SOLIDWORKS COM object unavailable"
    WScript.Quit 69
End If
WScript.Echo "Revision=" & sw.RevisionNumber
Set activeDoc = sw.ActiveDoc
If Err.Number <> 0 Then
    WScript.Echo "SOLIDWORKS ActiveDoc query failed=" & CStr(Err.Number) & ":" & Err.Description
    WScript.Quit 72
End If
If activeDoc Is Nothing Then
    WScript.Echo "ActiveDocIsNothing=True"
Else
    WScript.Echo "ActiveDocIsNothing=False"
End If
If (mode = "probe" Or mode = "exit") And Not activeDoc Is Nothing Then
    WScript.Echo "RefusingExit=ActiveDocumentPresent"
    WScript.Quit 70
End If
Err.Clear
Select Case mode
    Case "probe"
        result = 0
    Case "load"
        result = sw.LoadAddIn(dllPath)
        If Err.Number <> 0 Then
            WScript.Echo "SolidWorksCallFailed=" & CStr(Err.Number) & ":" & Err.Description
            WScript.Quit 71
        End If
        If result <> 0 And result <> 2 Then
            WScript.Echo "LoadAddInFailed=" & CStr(result)
            WScript.Quit 73
        End If
    Case "unload"
        result = sw.UnloadAddIn(dllPath)
        If Err.Number <> 0 Then
            WScript.Echo "SolidWorksCallFailed=" & CStr(Err.Number) & ":" & Err.Description
            WScript.Quit 71
        End If
        If result <> 0 And result <> 1 Then
            WScript.Echo "UnloadAddInFailed=" & CStr(result)
            WScript.Quit 74
        End If
    Case "exit"
        sw.ExitApp
        result = 0
End Select
If Err.Number <> 0 Then
    WScript.Echo "SolidWorksCallFailed=" & CStr(Err.Number) & ":" & Err.Description
    WScript.Quit 71
End If
WScript.Echo "Result=" & CStr(result)
WScript.Quit 0
