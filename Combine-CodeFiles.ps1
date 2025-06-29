# Set working directory to the location the script is run from
$workingDir = $PSScriptRoot

# Set the output file name with timestamp
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outputFile = Join-Path -Path $workingDir -ChildPath "Combined_$timestamp.txt"

# Define the file extensions to include
$extensions = @("*.cs", "*.xaml", "*.json", "*.csproj", "*.sln", "*.reg")  # Add more as needed

# Gather matching files recursively
$codeFiles = @()
foreach ($ext in $extensions) {
    $codeFiles += Get-ChildItem -Path $workingDir -Filter $ext -Recurse -File
}

# Combine file contents into one output file
foreach ($file in $codeFiles) {
    Add-Content -Path $outputFile -Value "`n`n// ----- $($file.FullName) -----`n"
    Get-Content -Path $file.FullName | Add-Content -Path $outputFile
}

Write-Output "Combined file created at: $outputFile"
