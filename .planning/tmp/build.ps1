$ErrorActionPreference = 'Continue'
Set-Location 'C:\Info\Project\DataMeasurement'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$args = @(
    'WPF_Example\DatumMeasurement.csproj',
    '/p:Configuration=Debug',
    '/p:Platform=x64',
    '/verbosity:minimal',
    '/nologo'
)
& $msbuild @args 2>&1 | Tee-Object -FilePath 'C:\Info\Project\DataMeasurement\.planning\tmp\build.log'
Write-Host "EXITCODE=$LASTEXITCODE"
exit $LASTEXITCODE
