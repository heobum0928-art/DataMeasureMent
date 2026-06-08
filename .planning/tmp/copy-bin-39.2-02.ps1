$src = 'C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug'
$dst = 'C:\Info\Project\DataMeasurement\.claude\worktrees\agent-a96ca5e4a48445e7f\WPF_Example\bin\x64\Debug'
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }
robocopy $src $dst /E /XO /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
$count = (Get-ChildItem $dst -Recurse -File).Count
Write-Output "files_in_dst=$count"
