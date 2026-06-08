$paths = @(
    'C:\Program Files (x86)\Microsoft Visual Studio',
    'C:\Program Files\Microsoft Visual Studio',
    'C:\Program Files (x86)\MSBuild',
    'C:\Windows\Microsoft.NET\Framework64',
    'C:\Windows\Microsoft.NET\Framework'
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Get-ChildItem -Path $p -Recurse -Filter MSBuild.exe -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    }
}
