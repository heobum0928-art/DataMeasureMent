$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Fix-DependsOn {
    param(
        [string]$Path,
        [string]$NewDep,
        [string]$Comment
    )
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $content = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($content.StartsWith([char]0xFEFF)) {
        $content = $content.Substring(1)
    }
    $oldLine = "wave: 2`ndepends_on: []`nfiles_modified:"
    $newLine = "wave: 2`ndepends_on: [$NewDep]  # B1 fix: $Comment`nfiles_modified:"
    if ($content.Contains($oldLine)) {
        $content = $content.Replace($oldLine, $newLine)
        $enc = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($Path, $content, $enc)
        Write-Output "OK: $Path"
    } else {
        Write-Output "NOT MATCHED: $Path"
    }
}

$base = 'C:\Info\Project\DataMeasurement\.planning\phases\39.2-urgent-additions-2-2026-05-30'
Fix-DependsOn -Path "$base\39.2-03-PLAN.md" -NewDep '"01", "02"' -Comment 'Plan 01 DualImageEdgeDistance TypeName + Plan 02 IntersectionPointSelection 빌드 후 실행'
Fix-DependsOn -Path "$base\39.2-04-PLAN.md" -NewDep '"01", "02"' -Comment 'Plan 04 Icon.Meas.DualImageEdgeDistance resource key 가 Plan 01 의 TypeName 등록 후 실행되도록 보장'
