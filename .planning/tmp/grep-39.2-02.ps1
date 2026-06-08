$file = 'C:\Info\Project\DataMeasurement\.claude\worktrees\agent-a96ca5e4a48445e7f\WPF_Example\Custom\Sequence\Inspection\Measurements\ArcLineIntersectDistanceMeasurement.cs'
$patterns = @(
    'public string IntersectionPointSelection { get; set; } = "Far";',
    'public List<string> IntersectionPointSelectionList',
    'new List<string> { "Far", "Close" }',
    '[ItemsSourceProperty(nameof(IntersectionPointSelectionList))]',
    'IntersectionPointSelection == "Close"',
    'bool useClose',
    'measurePointCol = int1Col',
    'measurePointRow = int1Row',
    'measurePointCol = int2Col',
    'measurePointRow = int2Row',
    'public string MeasureAxis { get; set; } = "X";',
    'VisionAlgorithmService.TryIntersectLines'
)
foreach ($p in $patterns) {
    $count = (Select-String -Path $file -Pattern $p -SimpleMatch).Count
    Write-Output ("{0,3} : {1}" -f $count, $p)
}
