$proj = 'C:\Info\Project\DataMeasurement\.claude\worktrees\agent-a96ca5e4a48445e7f\WPF_Example\DatumMeasurement.csproj'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$out = & $msbuild $proj /t:Build /p:Configuration=Debug /p:Platform=x64 /v:n /nologo 2>&1
$errorLines = $out | Select-String -Pattern ': error '
$warningLines = $out | Select-String -Pattern ': warning '
Write-Output "ERROR_COUNT=$($errorLines.Count)"
Write-Output "WARNING_COUNT=$($warningLines.Count)"
Write-Output "--- LAST 6 LINES ---"
$out | Select-Object -Last 6
