[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$ProjectId = "Buster",

    [Parameter(Position = 1)]
    [string]$ServerUrl = "https://pagetomovie-production.up.railway.app",

    [Parameter(Position = 2)]
    [string]$Secret = "longsecretHal2001576501!",

    [Parameter(Position = 3)]
    [string]$OutDir = "projects",

    [switch]$Extract = $true
)

$ErrorActionPreference = "Stop"

$baseUrl = $ServerUrl.TrimEnd('/')
$exportUrl = "$baseUrl/api/admin/projects/$ProjectId/export?me=$([Uri]::EscapeDataString($Secret))"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Downloading project '$ProjectId' from $baseUrl..." -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Cyan

$zipPath = Join-Path -Path $PSScriptRoot -ChildPath "$ProjectId`_export.zip"

try {
    Invoke-WebRequest -Uri $exportUrl -OutFile $zipPath -UserAgent "PageToMovie-PowerShell-Client"
    $fileInfo = Get-Item $zipPath
    Write-Host "Successfully downloaded $ProjectId export zip ($([math]::Round($fileInfo.Length / 1MB, 2)) MB)." -ForegroundColor Green

    if ($Extract) {
        $targetDir = Join-Path -Path $PSScriptRoot -ChildPath "$OutDir\$ProjectId"
        Write-Host "Extracting to $targetDir..." -ForegroundColor Yellow
        if (Test-Path $targetDir) {
            Remove-Item -Path $targetDir -Recurse -Force
        }
        Expand-Archive -Path $zipPath -DestinationPath $targetDir -Force
        Write-Host "Project '$ProjectId' downloaded and extracted cleanly to: $targetDir" -ForegroundColor Green
        Remove-Item -Path $zipPath -Force
    }
}
catch {
    Write-Error "Failed to download project '$ProjectId' from server: $_"
}
