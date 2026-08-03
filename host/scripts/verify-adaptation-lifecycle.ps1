[CmdletBinding()]
param(
    [switch]$AllowDirty,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $repoRoot
try {
    $commit = (& git rev-parse HEAD).Trim()
    $status = @(& git status --porcelain)
    if (-not $AllowDirty -and $status.Count -gt 0) {
        throw 'Final verification requires a clean working tree. Commit first or use -AllowDirty for a non-final preflight.'
    }

    $verificationDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PageToMovieVerification'
    New-Item -ItemType Directory -Force -Path $verificationDir | Out-Null
    $env:PAGETOMOVIE_LIVE_API_TESTS = '0'

    Write-Host "Commit: $commit"
    Write-Host "Working tree: $(if ($status.Count -eq 0) { 'clean' } else { 'dirty preflight' })"
    Write-Host 'Running complete offline solution suite (Category!=LiveApi)...'
    $testArgs = @(
        'test', 'host/PageToMovie.slnx',
        '-p:UseSharedCompilation=false',
        '--filter', 'Category!=LiveApi',
        '--results-directory', $verificationDir,
        '--logger', 'trx;LogFileName=adaptation-offline.trx'
    )
    if ($NoRestore) { $testArgs += '--no-restore' }
    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { throw "Offline solution suite failed with exit code $LASTEXITCODE." }

    Write-Host 'Running zero-cost ScreenplayBenchmark self-test...'
    & dotnet run --project host/tools/ScreenplayBenchmark -- --self-test
    if ($LASTEXITCODE -ne 0) { throw "ScreenplayBenchmark self-test failed with exit code $LASTEXITCODE." }

    Write-Host "Verification passed for $commit"
    Write-Host "TRX: $(Join-Path $verificationDir 'adaptation-offline.trx')"
}
finally {
    Pop-Location
}
