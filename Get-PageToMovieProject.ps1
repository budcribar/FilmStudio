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
$zipPath = Join-Path -Path $PSScriptRoot -ChildPath "$ProjectId`_export.zip"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Downloading project '$ProjectId' from $baseUrl..." -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Obtain Admin Bearer Token via Operator Secret
$token = $null
try {
    $loginUrl = "$baseUrl/api/auth/operator-override"
    $bodyJson = @{ secret = $Secret } | ConvertTo-Json
    $loginResp = Invoke-RestMethod -Uri $loginUrl -Method Post -ContentType "application/json" -Body $bodyJson
    if ($loginResp -and $loginResp.ok -and $loginResp.token) {
        $token = $loginResp.token
        Write-Host "Authenticated as admin via operator secret." -ForegroundColor Green
    }
}
catch {
    Write-Host "Operator override login skipped/failed; falling back to direct export link..." -ForegroundColor DarkGray
}

# 2. Download Project Export Zip
$headers = @{}
if ($token) {
    $headers["Authorization"] = "Bearer $token"
}

$exportUrls = @(
    "$baseUrl/api/projects/$ProjectId/export",
    "$baseUrl/api/admin/projects/$ProjectId/export",
    "$baseUrl/api/projects/$ProjectId/export?me=$([Uri]::EscapeDataString($Secret))",
    "$baseUrl/api/admin/projects/$ProjectId/export?me=$([Uri]::EscapeDataString($Secret))"
)

$downloaded = $false
foreach ($url in $exportUrls) {
    try {
        if ($headers.Count -gt 0) {
            Invoke-WebRequest -Uri $url -Headers $headers -OutFile $zipPath -UserAgent "PageToMovie-PowerShell-Client"
        }
        else {
            Invoke-WebRequest -Uri $url -OutFile $zipPath -UserAgent "PageToMovie-PowerShell-Client"
        }
        $downloaded = $true
        break
    }
    catch {
        # try next fallback URL
    }
}

if (-not $downloaded) {
    Write-Error "Failed to download project '$ProjectId' from server."
    return
}

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
