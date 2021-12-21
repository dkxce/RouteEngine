; ---------------------------------------------------------------------
;
; NSIS Installer Script
;                    min v3.0
; _AccessControl(NSIS_plugin) required
;
; written by Milok Zbrozek <milokz@gmail>
;
; ---------------------------------------------------------------------

Unicode True

; Include
!include "x64.nsh"
!include "MUI2.nsh"
!include "nsDialogs.nsh"
; ---------------------------------------------------------------------

; PROJECT CONSTANTS 
!define PRODUCT_BUILD "21.12.21.4"
!define PRODUCT_NAME "dkxce.Route.Engine"
!define COPYRIGHTS "Milok Zbrozek <milokz@gmail.com>"
!define INSTALLER "dkxce.Route.Installer"
!define CheckSysID "false" ; (false or true) use SSKeySys.exe to get Machine ID
!define CorrectSysID "EF4B-9EAB-F7A7-2389-501B-8CD7-A94C-6A6D"
!define LockPassword "0"
; ---------------------------------------------------------------------

; PROJECT MAIN INFO
BrandingText "${COPYRIGHTS}"
Caption "Установка ${PRODUCT_NAME} v${PRODUCT_BUILD}"
Name "${PRODUCT_NAME}"
Icon "..\ICONS\install.ico"
InstallDir "C:\ROUTES\"
InstallDirRegKey HKLM "Software\${PRODUCT_NAME}" "Install_Dir"
OutFile "${INSTALLER}_v${PRODUCT_BUILD}.exe"
; ---------------------------------------------------------------------
  
; Addit Params
ShowInstDetails show
RequestExecutionLevel admin
; ---------------------------------------------------------------------

; Main Variables
Var StartMenuFolder
Var CurrentSysID
Var isCorrectSysID
Var isInstalled
Var hCtl_PassBox
Var hCtl_PassBox_pass
Var hCtl_PassBox_Label1
Var PasswordEntered
; ---------------------------------------------------------------------

; Addit Variables
Var instServiceSolver
Var instRouteSvcState
Var instMapCreator
; ---------------------------------------------------------------------

; More Variables
Var dialog
Var hwnd
Var skippl
; ---------------------------------------------------------------------

; Custom Pages Configurations
!include "..\INSTRESOURCES\LoadRTF.nsh"
!include "..\INSTRESOURCES\xForms_noAdmin.nsdinc"
!include "..\INSTRESOURCES\xForms_SvcExists.nsdinc"
!include "..\INSTRESOURCES\xForms_wrongSysID.nsdinc"
!include "..\INSTRESOURCES\xForms_SysID.nsdinc"
!include "..\INSTRESOURCES\xForms_Ports.nsdinc"
!include "..\INSTRESOURCES\xForms_Shortcuts.nsdinc"
!include "..\INSTRESOURCES\xForms_onFinish.nsdinc"
; ---------------------------------------------------------------------

; Interface Settings
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "${NSISDIR}\Contrib\Graphics\Header\orange.bmp" 
!define MUI_ICON "..\ICONS\install.ico"
!define MUI_UNICON "..\ICONS\install.ico"
!define MUI_ABORTWARNING
; ---------------------------------------------------------------------

; Registry Settings
!define MUI_LANGDLL_REGISTRY_ROOT "HKCU" 
!define MUI_LANGDLL_REGISTRY_KEY "Software\${PRODUCT_NAME}" 
!define MUI_LANGDLL_REGISTRY_VALUENAME "Installer Language"
; ---------------------------------------------------------------------

; Start Menu Folder Page Configuration
!define MUI_STARTMENUPAGE_REGISTRY_ROOT "HKCU" 
!define MUI_STARTMENUPAGE_REGISTRY_KEY "Software\${PRODUCT_NAME}" 
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "Start Menu Folder"
; ---------------------------------------------------------------------

; FINISH Page Configuration
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Создать ярлык на рабочем столе"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION finishpageaction
; ---------------------------------------------------------------------

;Install Pages
!insertmacro MUI_PAGE_LICENSE "..\INSTRESOURCES\License.txt"
Page Custom EnterPasswordDialogShow EnterPasswordDialogLeave
Page Custom fnc_RTF_Show
Page Custom fnc_noAdmin_Show
Page Custom fnc_SysID_Preload
Page Custom fnc_SysID_Show
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_STARTMENU Application $StartMenuFolder
!insertmacro MUI_PAGE_INSTFILES
Page Custom fnc_xForms_Ports_Show fnc_xForms_Ports_Leave
Page Custom fnc_xForms_Shortcuts_Show fnc_xForms_Shortcuts_Leave
Page Custom fnc_xForms_onFinish_Show fnc_xForms_onFinish_Leave
!insertmacro MUI_PAGE_FINISH
; ---------------------------------------------------------------------

;Unistall Pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
; ---------------------------------------------------------------------

;Languages
!insertmacro MUI_LANGUAGE "Russian"
; ---------------------------------------------------------------------

; Set Permissions to Path
!macro GrandPermissions Path
	AccessControl::GrantOnFile \
		"${Path}" "NT AUTHORITY\SYSTEM" "FullAccess"
	Pop $0
	AccessControl::GrantOnFile \
		"${Path}" "NT AUTHORITY\LOCAL SERVICE" "FullAccess"
	Pop $0
	AccessControl::GrantOnFile \
		"${Path}" "NT AUTHORITY\NETWORK SERVICE" "FullAccess"
	Pop $0	
!macroend
; ---------------------------------------------------------------------

; Kill Process
!macro KillProcess AppName
  DetailPrint "Kill Process ${AppName}"   
  ${If} ${RunningX64}
		ExecWait '$TEMP\dkxce.Routes\nircmd_x64.exe killprocess ${AppName}'
  ${Else}
		ExecWait '$TEMP\dkxce.Routes\nircmd_x86.exe killprocess ${AppName}'
  ${EndIf}    
  Delete "$TEMP\dkxce.Routes\nircmd_x64.exe"
  Delete "$TEMP\dkxce.Routes\nircmd_x86.exe"
!macroend
; ---------------------------------------------------------------------

; Install Types
InstType "Полная" ; 1
InstType "Полная без шейпов" ; 2
InstType "Стандартная" ; 3
InstType "Стандартная без шейпов" ; 4
InstType "Creator (Создание графа)" ; 5
InstType "Solver (Поиск маршрутов)" ; 6
; ---------------------------------------------------------------------

; FILES SECTIONS
Section "!Main Components"
  SectionIn 1 2 3 4 5 6 RO
  
  WriteRegStr HKLM "SOFTWARE\${PRODUCT_NAME}" "Install_Dir" "$INSTDIR"  
  
  SetOutPath $INSTDIR    
  File "IRouteSolver.dll"  
  
  SetOutPath $INSTDIR\GRAPHS
  ;File /r "GRAPHS\*.*"      
  
  SetOutPath $INSTDIR\RGWays
  ;File /r "RGWays\*.*"      
        
  SetOutPath $INSTDIR\TOOLS
  File "TOOLS\Syslib.dll"
  ;File /r "TOOLS\*.*"
  
  SetOutPath $INSTDIR\Regions
  ;File /r "Regions\*.*"
  
  ; Write the uninstall keys for Windows
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "DisplayName" "dkxce.Route.ServiceSolver"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  
  SetOutPath $INSTDIR   
  WriteUninstaller "uninstall.exe"
  
  ; Shortcuts
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"  
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Uninstall"  
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Uninstall\Uninstall.lnk" "$INSTDIR\uninstall.exe" "" "$INSTDIR\uninstall.exe" 0    
  !insertmacro MUI_STARTMENU_WRITE_END  
  
  !insertmacro GrandPermissions "$INSTDIR"
SectionEnd

Section "!Regions Shapes"
  SectionIn 1 2 3 4 5 6
  
  SetOutPath $INSTDIR\Regions
  SetOverwrite off
  File /r "Regions\*.*"
  SetOverwrite on
SectionEnd

Section "!Map Creator"
  SectionIn 1 2 3 4 5
  
  SetOutPath $INSTDIR\MapCreator
  File /r "MapCreator\*.*"
  
  StrCpy $instMapCreator 1
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Map Creator.lnk" "$INSTDIR\MapCreator\RouteGraphBatcher.exe" "" "$INSTDIR\MapCreator\RouteGraphBatcher.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section "!ServiceSolver"
  SectionIn 1 2 3 4 6
  
  SetOutPath $INSTDIR\Service
  File /r "Service\*.*"
  
  StrCpy $instServiceSolver 1  
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"  
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\dkxce.Route.ServiceSolver Console.lnk" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" "" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" 0  
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\dkxce.Route.ServiceSolver Start.lnk" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" "/start" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" 0  
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\dkxce.Route.ServiceSolver Stop.lnk" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" "/stop" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section "!Route Service State"
  SectionIn 1 2 3 4 6
  
  SetOutPath $INSTDIR\TOOLS
  File /r "TOOLS\*.dll"  
  File /r "TOOLS\RouteServiceState.exe"
  
  StrCpy $instRouteSvcState 1  
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\RouteServiceState.lnk" "$INSTDIR\TOOLS\RouteServiceState.exe" "" "$INSTDIR\TOOLS\RouteServiceState.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section "Shapes Files Documentation"
  SectionIn 1 2 3 4 5
  
  SetOutPath $INSTDIR\_SHAPES_
  File "_SHAPES_\*.x*" 
  File "_SHAPES_\*.t*" 
  File "_SHAPES_\*.r*" 
SectionEnd

Section /o "Shapes Files Examples"
  SectionIn 1 3 5
  SetOutPath $INSTDIR\_SHAPES_
  File "_SHAPES_\*.z*" 
  File "..\INSTRESOURCES\7za.exe"
  
  DetailPrint "Unpacking Shapes Files Examples"  
  ExecWait '"$INSTDIR\_SHAPES_\7za.exe" e -y "$INSTDIR\_SHAPES_\_SHAPES_.zip"'
  
  Delete "$INSTDIR\_SHAPES_\_SHAPES_.z*"
  Delete "$INSTDIR\_SHAPES_\7za.exe"
SectionEnd

Section "Shapes BBox to Regions"
  SectionIn 1 2 3 4 5
  
  SetOutPath $INSTDIR\TOOLS
  File /r "TOOLS\*.dll"
  File /r "TOOLS\ShapesBBox2Regions.exe"
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Shape Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Shape Tools\ShapesBBox2Regions.lnk" "$INSTDIR\TOOLS\ShapesBBox2Regions.exe" "" "$INSTDIR\TOOLS\ShapesBBox2Regions.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section /o "Shapes Merger"
  SectionIn 1 2 5
  
  SetOutPath $INSTDIR\TOOLS
  File /r "TOOLS\*.dll"
  File /r "TOOLS\ShapesMerger.exe"
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Shape Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Shape Tools\ShapesMerger.lnk" "$INSTDIR\TOOLS\ShapesMerger.exe" "" "$INSTDIR\TOOLS\ShapesMerger.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section /o "Shapes Polygons Extractor"
  SectionIn 1 2 5

  SetOutPath $INSTDIR\TOOLS
  File /r "TOOLS\*.dll"
  File /r "TOOLS\ShapesPolygonsExtractor.exe"
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Shape Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Shape Tools\ShapesPolygonsExtractor.lnk" "$INSTDIR\TOOLS\ShapesPolygonsExtractor.exe" "" "$INSTDIR\TOOLS\ShapesPolygonsExtractor.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section /o "Shapes Viewer"
  SectionIn 1 2 5
  
  SetOutPath $INSTDIR    
  File "ShapeViewer.exe"  
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Shape Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Shape Tools\Shape Viewer.lnk" "$INSTDIR\ShapeViewer.exe" "" "$INSTDIR\ShapeViewer.exe" 0    
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section "!DBF Tools"
  SectionIn 1 2 3 4 5
  
  SetOutPath $INSTDIR\DBF_TOOLS
  File /r "DBF_TOOLS\*.*" 
  File "..\INSTRESOURCES\7za.exe"
  
  DetailPrint "Unpacking DBF TOOLS"  
  ExecWait '"$INSTDIR\DBF_TOOLS\7za.exe" e -y "$INSTDIR\DBF_TOOLS\DBF_TOOLS.zip"'
  Delete "$INSTDIR\DBF_TOOLS\DBF_TOOLS.zip"
  Delete "$INSTDIR\DBF_TOOLS\7za.exe"
  
  ; Shortcuts
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\DBF Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\DBF Tools\DBF Navigator.lnk" "$INSTDIR\DBF_Tools\DBFNavigator.exe" "" "$INSTDIR\DBF_Tools\DBFNavigator.exe" 0    
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\DBF Tools\DBF Show.lnk" "$INSTDIR\DBF_Tools\DBFShow.exe" "" "$INSTDIR\DBF_Tools\DBFShow.exe" 0    
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\DBF Tools\SDBF.lnk" "$INSTDIR\DBF_Tools\SDBF.exe" "" "$INSTDIR\DBF_Tools\SDBF.exe" 0    
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section /o "Test Tools"
  SectionIn 1 2 5
  
  SetOutPath $INSTDIR\TEST
  File /r "TEST\*.*"
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Test Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Test Tools\Map Route Test.lnk" "$INSTDIR\TEST\mapRouteTest.exe" "" "$INSTDIR\TEST\mapRouteTest.exe" 0  
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Test Tools\NMS Routes Web Test.lnk" "$INSTDIR\TEST\nmsRoutesWebTest.exe" "" "$INSTDIR\TEST\nmsRoutesWebTest.exe" 0  
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Test Tools\Working Load Test.lnk" "$INSTDIR\TEST\WorkingLoadTest.exe" "" "$INSTDIR\TEST\WorkingLoadTest.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section /o "RGWay2RTE"
  SectionIn 1 2 5
  
  SetOutPath $INSTDIR\TOOLS
  File /r "TOOLS\*.dll"
  File /r "TOOLS\RGWay2RTE.exe"
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Tools\RGWay2RTE.lnk" "$INSTDIR\TOOLS\RGWay2RTE.exe" "" "$INSTDIR\TOOLS\RGWay2RTE.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section /o "Routes KeyGen"
  SectionIn 1 2 3 4
  
  SetOutPath $INSTDIR\TOOLS
  File /r "TOOLS\*.dll"
  File /r "TOOLS\RoutesKeyGen.exe"
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Tools\RoutesKeyGen.lnk" "$INSTDIR\TOOLS\RoutesKeyGen.exe" "" "$INSTDIR\TOOLS\RoutesKeyGen.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd

Section /o "System Information - SSKeySys"
  SectionIn 1 2 3 4
  
  SetOutPath $INSTDIR\TOOLS
  File /r "TOOLS\SSKeySys.exe"
  
  !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
  CreateDirectory "$SMPROGRAMS\$StartMenuFolder\Tools"
  CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Tools\SSKeySys.lnk" "$INSTDIR\TOOLS\SSKeySys.exe" "" "$INSTDIR\TOOLS\SSKeySys.exe" 0  
  !insertmacro MUI_STARTMENU_WRITE_END  
SectionEnd
; ---------------------------------------------------------------------

; Uninstall
Section "Uninstall"
  !insertmacro KillProcess "RouteServiceState.exe"
  ExecWait '"$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" /stop'
  !insertmacro KillProcess "dkxce.Route.ServiceSolver.exe"
  ExecWait '"$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" /u'
  !insertmacro KillProcess "dkxce.Route.ServiceSolver.exe"
  
  Delete "$DESKTOP\Map Creator.lnk"
  Delete "$DESKTOP\dkxce.Route.ServiceSolver.lnk"
  Delete "$DESKTOP\RouteServiceState.lnk"
  
  !insertmacro MUI_STARTMENU_GETFOLDER Application $StartMenuFolder
  RMDir /r "$SMPROGRAMS\$StartMenuFolder"
  RMDir /r $INSTDIR    
SectionEnd
; ---------------------------------------------------------------------

; On Init Function
Function .onInit
    !insertmacro MUI_LANGDLL_DISPLAY
	
	StrCpy $PasswordEntered ${LockPassword}
	StrCpy $skippl 0
	StrCpy $instServiceSolver 0
	StrCpy $instRouteSvcState 0
	StrCpy $instMapCreator 0	
	
	SetCurInstType 3	
	
	InitPluginsDir
	
	; Prevent Multiples Install
	System::Call 'kernel32::CreateMutex(p 0, i 0, t "${PRODUCT_NAME}") p .r1 ?e'
	Pop $R0
	
	; Only One Instance
	StrCmp $R0 0 +3
    MessageBox MB_OK|MB_ICONEXCLAMATION "Программа установки уже запущена!"
    Abort
FunctionEnd
; ---------------------------------------------------------------------

; Загрузка System Info
Function fnc_SysID_Preload
	${If} $skippl == 0
		StrCpy $skippl 1
		
		!insertmacro MUI_HEADER_TEXT "Информация о системе" "Подождите... Идет сбор информации о системе..."
		
		; disable next button #1 - Next, 2 - Cancel, 3 - Back
		GetDlgItem $1 $HWNDPARENT 1
		EnableWindow $1 0
		GetDlgItem $3 $HWNDPARENT 3
		EnableWindow $3 0		
		
		; unpack syslib
		SetOutPath "$TEMP\dkxce.Routes"
		File "TOOLS\Syslib.dll" 
		
		; check Drives		
			System::Call 'Syslib.dll::GetInstallPath(v) t.r0'
			StrCpy $INSTDIR $0
		; check Installed				
			System::Call 'Syslib.dll::ServiceIsInstalled(v) b.r0'
			StrCpy $isInstalled $0
			${If} $isInstalled == 1
				Call fnc_xForms_SvcExists_Show
			${EndIf}	
		; check System_ID			
			${If} ${CheckSysID} == "true"
				StrCpy $isCorrectSysID "false"				
				System::Call 'Syslib.dll::GetSysId(v) t.r0'
				StrCpy $CurrentSysID $0				
				${If} $CurrentSysID == ${CorrectSysID}
					StrCpy $isCorrectSysID "true"
				${EndIf}	
			${Else}	
				StrCpy $isCorrectSysID "true"
			${EndIf}	
		; end check	
		Delete "$TEMP\dkxce.Routes\Syslib.dll"
		
		; enable next button #1 - Next, 2 - Cancel, 3 - Back
		GetDlgItem $1 $HWNDPARENT 1
		EnableWindow $1 1
		GetDlgItem $3 $HWNDPARENT 3
		EnableWindow $3 1
	${EndIf}	
FunctionEnd
; ---------------------------------------------------------------------

; On Uninstall Init
Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE  
  SetOutPath "$TEMP\dkxce.Routes"    
  File "..\INSTRESOURCES\nircmd_x64.exe"
  File "..\INSTRESOURCES\nircmd_x86.exe"
FunctionEnd
; ---------------------------------------------------------------------

; Отображение System ID
Function fnc_SysID_Show
	${If} $isCorrectSysID != "true"  		
		Call fnc_wrongSysID_Create
		nsDialogs::Show
	${Else}
		${If} ${CheckSysID} == "true"
			Call fnc_xForms_SysID_Create
			nsDialogs::Show
		${EndIf}	
	${EndIf}	
FunctionEnd
; ---------------------------------------------------------------------

; Диалог RTF
Function fnc_RTF_Show
  !insertmacro MUI_HEADER_TEXT "Об установке" "Краткое описание возможностей программы"
  
  SetOutPath "$TEMP\dkxce.Routes"
  File "..\INSTRESOURCES\Readme.rtf"

  nsDialogs::Create 1018
  Pop $dialog
  ${If} $dialog == error
    Abort
  ${EndIf}
 
  nsDialogs::CreateControl "RichEdit20A" \
    ${ES_READONLY}|${WS_VISIBLE}|${WS_CHILD}|${WS_TABSTOP}|${WS_VSCROLL}|${ES_MULTILINE}|${ES_WANTRETURN} \
	${WS_EX_STATICEDGE} \
	0 0 100% 100% ''
  Pop $hwnd
 
  /* Load an RTF file into the control */
  ${LoadRTF} "$TEMP\dkxce.Routes\Readme.rtf" $hwnd
 
  nsDialogs::Show
FunctionEnd
; ---------------------------------------------------------------------

; desktop shortcuts
Function finishpageaction
	DetailPrint "Create desktop shortcuts"  
	${If} $instMapCreator == 1
		CreateShortcut "$DESKTOP\Map Creator.lnk" "$INSTDIR\MapCreator\RouteGraphBatcher.exe" "" "$INSTDIR\MapCreator\RouteGraphBatcher.exe" 0  
	${EndIf}
	${If} $instServiceSolver == 1
		CreateShortcut "$DESKTOP\dkxce.Route.ServiceSolver.lnk" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" "" "$INSTDIR\Service\dkxce.Route.ServiceSolver.exe" 0  
	${EndIf}
	${If} $instRouteSvcState == 1
		CreateShortcut "$DESKTOP\RouteServiceState.lnk" "$INSTDIR\TOOLS\RouteServiceState.exe" "" "$INSTDIR\TOOLS\RouteServiceState.exe" 0  
	${EndIf}  
FunctionEnd
; ---------------------------------------------------------------------

; EnterPassword
Function EnterPasswordDialogShow
  ${If} $PasswordEntered != "0"
    nsDialogs::Create 1018
    Pop $hCtl_PassBox
    ${If} $hCtl_PassBox == error
      Abort
    ${EndIf}
    !insertmacro MUI_HEADER_TEXT "Ввод цифрового ключа" "Введите цифровой ключ для данного ПО"  
    ${NSD_CreateLabel} 21.06u 30.77u 226.43u 11.69u "Для установки данного ПО требуется цифровой ключ:"
    Pop $hCtl_PassBox_Label1  
    ${NSD_CreatePassword} 21.06u 44.31u 252.1u 12.31u ""
    Pop $hCtl_PassBox_pass  
	; disable next button #1 - Next, 2 - Cancel, 3 - Back
	GetDlgItem $1 $HWNDPARENT 3
	EnableWindow $1 0
	nsDialogs::Show
  ${EndIf}  
FunctionEnd
Function EnterPasswordDialogLeave
 ${NSD_GetText} $hCtl_PassBox_pass $0
 ${If} $0 == ${LockPassword}
    StrCpy $PasswordEntered 0
 ${Else}
    MessageBox MB_OK|MB_ICONEXCLAMATION "Вы ввели неверный цифровой ключ"
	Abort
 ${EndIf} 
FunctionEnd
; ---------------------------------------------------------------------