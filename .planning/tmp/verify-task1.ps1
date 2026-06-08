$f = 'WPF_Example/Halcon/Algorithms/DatumFindingService.cs'
$newOverload = (Select-String -Path $f -Pattern 'TryFindDatum\(HImage imageHorizontal' -AllMatches | Measure-Object).Count
$newMethod = (Select-String -Path $f -Pattern 'TryFindVerticalTwoHorizontalDualImage' -AllMatches | Measure-Object).Count
$existingSig = (Select-String -Path $f -Pattern 'TryFindDatum\(HImage image, DatumConfig config' -AllMatches | Measure-Object).Count
$vertGetSize = (Select-String -Path $f -Pattern 'imageVertical\.GetImageSize' -AllMatches | Measure-Object).Count
$horizGetSize = (Select-String -Path $f -Pattern 'imageHorizontal\.GetImageSize' -AllMatches | Measure-Object).Count
$tryFindLineVert = (Select-String -Path $f -Pattern 'imageVertical, imageVerticalWidth' -AllMatches | Measure-Object).Count
$tryExtractEdgeHoriz = (Select-String -Path $f -Pattern 'imageHorizontal, imageHorizontalWidth' -AllMatches | Measure-Object).Count
$hbkCount = (Select-String -Path $f -Pattern '//260527 hbk Phase 34 D-34' -AllMatches | Measure-Object).Count
Write-Host "newOverload=$newOverload newMethod=$newMethod existingSig=$existingSig vertGetSize=$vertGetSize horizGetSize=$horizGetSize tryFindLineVert=$tryFindLineVert tryExtractEdgeHoriz=$tryExtractEdgeHoriz hbkCount=$hbkCount"
if ($newOverload -eq 1 -and $newMethod -ge 2 -and $existingSig -eq 1 -and $vertGetSize -eq 1 -and $horizGetSize -eq 1 -and $tryFindLineVert -eq 1 -and $tryExtractEdgeHoriz -eq 2 -and $hbkCount -ge 5) {
    Write-Host 'PASS Task 1'
} else {
    Write-Host 'FAIL Task 1'
    exit 1
}
