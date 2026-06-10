@echo off
cd /d C:\Info\Project\DataMeasurement
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /verbosity:minimal /nologo > C:\Info\Project\DataMeasurement\.planning\tmp\build.log 2>&1
echo EXITCODE=%ERRORLEVEL%
exit /b %ERRORLEVEL%
