; NSIS 3.x install script for 'Satisfactory Savegame Tool'
;
; (c)2019 SillyBits
;

Unicode true


; Application specific settings
!define APPNAME           "Satisfactory Savegame Tool"
!define APPNAMESHORT      "SatisfactorySavegameTool"
!define APPVERSION        "0.1 alpha"
!define APPVERSIONSHORT   "0.1alpha"
!define APPICON           "..\App\Resources\Logo-128x128.ico"

LangString APPNAMEDESKTOP 1033 "Satisfactory Savegame Tool - ${APPVERSION}"
LangString APPNAMEDESKTOP 1031 "Satisfactory Spielstand Helfer - ${APPVERSION}"
LangString APPUNINSTALL   1033 "Uninstall"
LangString APPUNINSTALL   1031 "Deinstallieren"
LangString APPWINVERFAIL  1033 "Sorry to say, but this version of Windows isn't supported anymore."
LangString APPWINVERFAIL  1031 "Es tut mir leid, aber diese Version von Windows wird leider nicht mehr unterstützt."
LangString APPNO64BITOS   1033 "Sorry to say, but this software requires a 64bit version of Windows."
LangString APPNO64BITOS   1031 "Es tut mir leid, aber dieses Programm benötigt eine 64bit Version von Windows."

!define APPNAMEANDVERSION "$(APPNAMEDESKTOP) - ${APPVERSION}"

!define STARTMENUFOLDER   "$(APPNAMEDESKTOP)"

!define REGISTRYROOT      HKLM
!define REGISTRYKEY       "Software\SillyBits\${APPNAME}"

!define SOURCEDIR         ".\__delivery"
!define PREREQUISITESDIR  ".\__prerequisites"

!define FORCE_64BIT


; Main settings
Name "${APPNAMEANDVERSION}"
InstallDir "$PROGRAMFILES\${APPNAME}"
InstallDirRegKey ${REGISTRYROOT} "${REGISTRYKEY}" ""
OutFile "Setup-${APPNAMESHORT}-v${APPVERSIONSHORT}.exe"

BrandingText "(c)2019 SillyBits"

SetCompressor /SOLID /FINAL LZMA

SetDateSave on
SetOverwrite ifnewer

ManifestSupportedOS Win7 Win8 Win8.1 Win10 ; Just in case default values are changed
ManifestDPIAware true

RequestExecutionLevel user ;admin


; Modern interface settings
!include "MUI2.nsh"

!include "x64.nsh"
!include "WinVer.nsh"
!include "nsProcess.nsh"


; Allow for full & custom install type, with components only visible with custom selection
; Whats allowed is being decided at runtime based on cmdline switch "/custom"
; TODO: Add code for supporting /custom when more component sections avail
InstType "Full installation"
InstType /COMPONENTSONLYONCUSTOM

!define MUI_ICON   "${APPICON}"
!define MUI_UNICON "${APPICON}"

!define MUI_LANGDLL_REGISTRY_ROOT      ${REGISTRYROOT}
!define MUI_LANGDLL_REGISTRY_KEY       "${REGISTRYKEY}"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallLanguage"

!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
;!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; Set languages (first is default language)
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "German"

!insertmacro MUI_RESERVEFILE_LANGDLL


; Startup "Install"
Function .onInit

	; Make sure we're started on an OS supported
	${IfNot} ${AtLeastWin7}
		${IfNot} ${Silent}
			MessageBox MB_ICONSTOP|MB_TOPMOST|MB_SETFOREGROUND "$(APPWINVERFAIL)"
		${EndIf}
		SetErrorLevel 7
		Quit
	${EndIf}

!ifdef FORCE_64BIT
	; Make sure we're on 64bit
	${IfNot} ${RunningX64}
		${IfNot} ${Silent}
			MessageBox MB_ICONSTOP|MB_TOPMOST|MB_SETFOREGROUND "$(APPNO64BITOS)"
		${EndIf}
		SetErrorLevel 64
		Quit
	${EndIf}
!endif

	${If} ${Silent}
		; Wait until application ended
		${Do}
			${nsProcess::FindProcess} "${APPNAMESHORT}.exe" $0
			${If} $0 == 603
				${Break}
			${ElseIf} $0 != 0
				SetErrorLevel $0
				Quit
			${EndIf}
			Sleep 500
		${Loop}
	${EndIf}

!ifdef FORCE_64BIT
	; Instruct registry methods NOT to use Wow6432Node-redirection
	SetRegView 64
!endif

	${IfNot} ${Silent}
		; Present language selector
		!insertmacro MUI_LANGDLL_DISPLAY
	${EndIf}

FunctionEnd

; Startup "Uninstall"
Function un.onInit

!ifdef FORCE_64BIT
	; Instruct registry methods NOT to use Wow6432Node-redirection
	SetRegView 64
!endif

	; Read last language selection, ...
	!insertmacro MUI_UNGETLANGUAGE
	; ..., and present language selector if no language was found
	!insertmacro MUI_LANGDLL_DISPLAY

FunctionEnd


Section InstallMain Install_Main

	; Gathering files needed is to be dealt with in control script!


	; Set Section properties
	SetOverwrite on

	; Set Section Files and Shortcuts
	SetOutPath "$INSTDIR"
	File /r "${SOURCEDIR}\*.*"

	SetShellVarContext all ; for ALL users
	CreateDirectory "$SMPROGRAMS\${STARTMENUFOLDER}"
	CreateShortCut "$SMPROGRAMS\${STARTMENUFOLDER}\$(APPNAMEDESKTOP).lnk" "$INSTDIR\${APPNAMESHORT}.exe"

	SetOutPath "$INSTDIR"

SectionEnd ; Main


; This section is mandatory and therefore hidden
; (Note: Using same section/function template as with modules to keep it streamlined)
Section "-Create Uninstaller" Install_Uninstaller

	; Set Section properties
	SetOverwrite on

	; Even if hidden, instruct 'checkmark not to be removed'
	SectionIn RO

	; Set Section Files
	WriteUninstaller "$INSTDIR\uninstall.exe"

	; Set Section Shortcuts
	SetShellVarContext all ; for ALL users
	CreateDirectory "$SMPROGRAMS\${STARTMENUFOLDER}"
	SetOutPath "$INSTDIR" ; Explicit selection of path used as working directory as this might have been changed earlier
	CreateShortCut "$SMPROGRAMS\${STARTMENUFOLDER}\$(APPUNINSTALL).lnk" "$INSTDIR\uninstall.exe"

	; Get size of installation, which is a bit tricky as components might have been deselected if installer was started using /custom switch
	Push $0
	Var /GLOBAL TotalDiskSpaceUsed
	StrCpy $TotalDiskSpaceUsed 0
	${If} ${SectionIsSelected} ${Install_Main}
		SectionGetSize ${Install_Main} $0 
		IntOp $TotalDiskSpaceUsed $TotalDiskSpaceUsed + $0
	${EndIf}
	IntOp $TotalDiskSpaceUsed $TotalDiskSpaceUsed + 100 ; Add another 100kb for uninstaller
	Pop $0

	; Set Section Registry
	WriteRegStr ${REGISTRYROOT} "${REGISTRYKEY}" ""               "$INSTDIR"
	WriteRegStr ${REGISTRYROOT} "${REGISTRYKEY}" "CurrentVersion" "${APPVERSION}"
	WriteRegStr HKLM            "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAMESHORT}" "DisplayName"     "$(APPNAMEANDVERSION)"
	WriteRegStr HKLM            "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAMESHORT}" "UninstallString" "$INSTDIR\uninstall.exe"
	WriteRegStr HKLM            "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAMESHORT}" "DisplayIcon"     "$INSTDIR\uninstall.exe"
	WriteRegStr HKLM            "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAMESHORT}" "Publisher"       "SillyBits"
	WriteRegDWORD HKLM          "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAMESHORT}" "EstimatedSize" $TotalDiskSpaceUsed

	; Save language selection
	!insertmacro MUI_LANGDLL_SAVELANGUAGE

SectionEnd
Function un.Install_Uninstaller

	; Remove from registry (incl. language selection) ...
	DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAMESHORT}"
	DeleteRegValue ${MUI_LANGDLL_REGISTRY_ROOT} "${MUI_LANGDLL_REGISTRY_KEY}" "${MUI_LANGDLL_REGISTRY_VALUENAME}"
	;DeleteRegKey ${MUI_LANGDLL_REGISTRY_ROOT} "${MUI_LANGDLL_REGISTRY_KEY}" ;=> Normally, this is same destination as below
	DeleteRegKey ${REGISTRYROOT} "${REGISTRYKEY}"

	; Delete Shortcuts
	SetShellVarContext all ; for ALL users
	Delete "$SMPROGRAMS\${STARTMENUFOLDER}\Uninstall.lnk"
	RMDir "$SMPROGRAMS\${STARTMENUFOLDER}"

	; Delete self
	Delete "$INSTDIR\uninstall.exe"

FunctionEnd


; Modern install component descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN

	!insertmacro MUI_DESCRIPTION_TEXT ${Install_Main}        "Main program files"
	!insertmacro MUI_DESCRIPTION_TEXT ${Install_Uninstaller} "Uninstaller"

!insertmacro MUI_FUNCTION_DESCRIPTION_END

; Uninstall section
Section Uninstall

	;!insertmacro MUI_DESCRIPTION_TEXT ${Install_Main}        "Main program files"
	Call un.Install_Uninstaller

	; Final clean up ... no "/r" in case user added files him-/herself
	RMDir "$INSTDIR"

SectionEnd

;; Finally, sign installer
;!finalize '"${CERTBAT}" "%1"'

; eof