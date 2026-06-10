$ErrorActionPreference = 'Continue'
$msb = Get-ChildItem 'C:\Program Files (x86)\Microsoft Visual Studio\*\*\MSBuild\*\Bin\MSBuild.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
Write-Host "MSBuild: $msb"
if ($msb) {
    & $msb 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /v:minimal
    exit $LASTEXITCODE
} else {
    Write-Host 'MSBuild not found - fallback dotnet build'
    dotnet build 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' -c Debug
    exit $LASTEXITCODE
}
