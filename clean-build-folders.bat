@echo off
setlocal

set "ROOT=%~dp0"

call :DeleteDir "%ROOT%.vs"
call :DeleteDir "%ROOT%VSPackage\bin"
call :DeleteDir "%ROOT%VSPackage\obj"
call :DeleteDir "%ROOT%VSPackage_IntegrationTests\bin"
call :DeleteDir "%ROOT%VSPackage_IntegrationTests\obj"
call :DeleteDir "%ROOT%VSPackage_UnitTests\bin"
call :DeleteDir "%ROOT%VSPackage_UnitTests\obj"

echo Fertig.
exit /b 0

:DeleteDir
if exist "%~1" (
    echo Loesche %~1
    rmdir /s /q "%~1"
) else (
    echo Uebersprungen, nicht gefunden: %~1
)
exit /b 0
