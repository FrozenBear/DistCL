@echo off

set BUILD_CONFIGURATION=%1

if "%BUILD_CONFIGURATION%"=="" set BUILD_CONFIGURATION=Debug

set PROJECT_PATH=%~dp0..\

cd /d %PROJECT_PATH%\bin\%BUILD_CONFIGURATION%

for %%I in (*.exe *.dll *.pdb) do copy /y %%I %PROJECT_PATH%\Tests\1
for %%I in (*.exe *.dll *.pdb) do copy /y %%I %PROJECT_PATH%\Tests\2
for %%I in (*.exe *.dll *.pdb) do copy /y %%I %PROJECT_PATH%\Tests\3
                                                    
start %PROJECT_PATH%\Tests\1\DistCL.ConsoleTest.exe
start %PROJECT_PATH%\Tests\2\DistCL.ConsoleTest.exe
start %PROJECT_PATH%\Tests\3\DistCL.ConsoleTest.exe
