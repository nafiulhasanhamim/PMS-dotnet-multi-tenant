#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Installs Git pre-commit hooks for the PMS project.

.DESCRIPTION
    This script installs a Git pre-commit hook that runs architecture tests
    before allowing commits. This ensures that clean architecture rules are
    enforced at commit time.

.EXAMPLE
    .\install-hooks.ps1
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$hookPath = Join-Path $repoRoot ".git/hooks/pre-commit"

$hookContent = @'
#!/bin/sh
echo "Running architecture tests..."
dotnet test tests/PMS.ArchitectureTests --no-build --verbosity quiet
if [ $? -ne 0 ]; then
    echo ""
    echo "=========================================="
    echo "Architecture tests failed! Commit aborted."
    echo "=========================================="
    echo ""
    echo "Please fix the architecture violations before committing."
    echo "Run 'dotnet test tests/PMS.ArchitectureTests' for details."
    exit 1
fi
echo "Architecture tests passed."
'@

# Check if we're in a git repository
$gitDir = Join-Path $repoRoot ".git"
if (-not (Test-Path $gitDir)) {
    Write-Host "Error: Not a git repository!" -ForegroundColor Red
    Write-Host "Please initialize git first with 'git init'" -ForegroundColor Yellow
    exit 1
}

# Create hooks directory if it doesn't exist
$hooksDir = Join-Path $gitDir "hooks"
if (-not (Test-Path $hooksDir)) {
    New-Item -ItemType Directory -Path $hooksDir -Force | Out-Null
}

# Write the hook file
Set-Content -Path $hookPath -Value $hookContent -Encoding UTF8

# Make the hook executable (for Unix-like systems)
if ($IsLinux -or $IsMacOS) {
    chmod +x $hookPath
}

Write-Host ""
Write-Host "Git pre-commit hook installed successfully!" -ForegroundColor Green
Write-Host "Location: $hookPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "The hook will run architecture tests before each commit." -ForegroundColor Yellow
