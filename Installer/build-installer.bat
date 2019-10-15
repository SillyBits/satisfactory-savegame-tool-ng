@echo off
echo [%TIME%] Creating installer, this might take a while

echo.

rem ----------
rem Mandatory stuff

set BASEINSTDIR=%~dp0

rem Make sure at least NSIS 3.02.1 is installed
set NSISEXE="c:\Program Files (x86)\NSIS\makensis.exe"


rem ----------
rem Collect all files needed

echo [%TIME%] Collecting files ...

if exist __delivery (
	rmdir /S /Q __delivery
	if %errorlevel% neq 0 (
		echo [%TIME%] Error deleting old __delivery folder
		exit /b -1
	)
	if exist __delivery (
		echo [%TIME%] Error deleting old __delivery folder
		exit /b -1
	)
)
mkdir __delivery
if not exist __delivery (
	echo [%TIME%] Error creating __delivery folder
	exit /b -1
)
cd __delivery
set ROOTDIR=%~dp0\..


copy %ROOTDIR%\App\bin\x64\Release\SatisfactorySavegameTool.exe .
copy %ROOTDIR%\App\bin\x64\Release\SatisfactorySavegameTool.exe.config .

copy %ROOTDIR%\App\bin\x64\Release\CoreLib.dll .
copy %ROOTDIR%\App\bin\x64\Release\FileHandler.dll .
copy %ROOTDIR%\App\bin\x64\Release\SavegameHandler.dll .

rem 3rd party libs
copy %ROOTDIR%\App\bin\x64\Release\ICSharpCode.TreeView.dll .

mkdir Resources

mkdir Resources\en-US
copy %ROOTDIR%\App\Resources\en-US\*.res .\Resources\en-US

mkdir Resources\de-DE
copy %ROOTDIR%\App\Resources\de-DE\*.res .\Resources\de-DE

copy %ROOTDIR%\App\Resources\*.xml .\Resources


set ROOTDIR=
cd ..

echo [%TIME%] ... done

echo.


rem ----------
rem Installer compilation

set BASEPARAMS=/V4

echo [%TIME%] Creating installer ...
set NSIFILE=SatisfactorySavegameToolNG.nsi
set LOGFILE=%NSIFILE%.log
%NSISEXE% %BASEPARAMS% %NSIFILE% -- > %LOGFILE%
if not errorlevel 0 (
	echo [%TIME%] Error compiling %NSIFILE%
	echo See %LOGFILE% for additional info
	pause
	exit /b -1
)
if not exist Setup*.exe (
	echo [%TIME%] Error compiling %NSIFILE%
	echo See %LOGFILE% for additional info
	pause
	exit /b -1
)
echo [%TIME%] ... done

echo.


rem ----------
rem Fin
:fin

echo [%TIME%] All done

echo.

pause
