Unicode true

;declare version
!define VER "1.0.13.4"

LoadLanguageFile "${NSISDIR}\Contrib\Language files\English.nlf"
;--------------------------------
;Version Information
VIProductVersion "${VER}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "BlueBrick"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductVersion" "${VER}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "ViraInsight LLC"
VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "2024 ViraInsight LLC"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "SolidWorks Addon for ViraInsight Engineering"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "${VER}"
;--------------------------------

;define installer name
OutFile "BlueBrick_install_${VER}.exe"

;set desktop as install directory
InstallDir "C:\BlueBrick"

;Request application privileges for Windows Vista and higher
RequestExecutionLevel admin

;page order
Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

;default section start
Section

;define output path
SetOutPath $INSTDIR

;specify file to go in output path
File /r /x *.pdb /x *.nsi *.*

;register dll
ExecWait '"$WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "$INSTDIR\BlueBrick.dll"'

;define uninstaller name
WriteUninstaller "$INSTDIR\uninstaller.exe"

;-------
;default section end
SectionEnd

;create a section to define what the uninstaller does.
;the section will always be named "Uninstall"
Section "Uninstall"

;delete installed file
Delete "$INSTDIR\BlueBrick.dll"
Delete "$INSTDIR\BlueBrick.png"
Delete "$INSTDIR\EPDM.Interop.epdm.dll"
Delete "$INSTDIR\EPDM.Interop.EPDMResultCode.dll"
Delete "$INSTDIR\PdfiumViewer.dll"
Delete "$INSTDIR\PdfiumViewer.xml"
Delete "$INSTDIR\PdfSharp.dll"
Delete "$INSTDIR\qryEngineers.xml"
Delete "$INSTDIR\qryUnitExport.xml"
Delete "$INSTDIR\settings.xml"
Delete "$INSTDIR\SolidWorks.Interop.sldworks.dll"
Delete "$INSTDIR\SolidWorks.Interop.swconst.dll"
Delete "$INSTDIR\SolidWorks.Interop.swpublished.dll"
Delete "$INSTDIR\solidworkstools.dll"
Delete "$INSTDIR\\x64\pdfium.dll"
Delete "$INSTDIR\\x86\pdfium.dll"

;delete the uninstaller
Delete "$INSTDIR\uninstaller.exe"

;delete the directory
RMDir $INSTDIR
SectionEnd