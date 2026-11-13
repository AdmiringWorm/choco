#Requires -PSEdition Core

<#
.SYNOPSIS
    Prepares a product line's 7-Zip update on its work branch.

.DESCRIPTION
    In a checkout of the target branch, creates the work branch update-7zip-<line> from the
    current commit (resetting it when it already exists), runs that branch's own
    Update-7zipVersion.ps1 with the version, and stages every change. Returns the work branch,
    whether anything changed, and the changed files. Nothing is committed or pushed.

    Refuses a checkout with uncommitted changes, so nothing is lost. Use -WhatIf to see what would
    happen. Needs 7-Zip (7z) on the PATH, like Update-7zipVersion.ps1 does.

.PARAMETER Path
    The checkout of the target branch.

.PARAMETER Line
    The product line, 1.x or 2.x. Names the work branch.

.PARAMETER Version
    The 7-Zip version to update to. Defaults to the latest release on GitHub. It is always passed
    to Update-7zipVersion.ps1 explicitly, because older copies of that script have no latest lookup.

.EXAMPLE
    ./.github/scripts/Update-7zipWorkBranch.ps1 -Path ../choco-support -Line 1.x -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Path = '.',

    [Parameter(Mandatory)]
    [ValidateSet('1.x', '2.x')]
    [string] $Line,

    [string] $Version
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if (-not $Version) {
    $Version = & "$PSScriptRoot/Get-7zipLatestVersion.ps1"
}

$workBranch = "update-7zip-$Line"
$Path = (Resolve-Path -LiteralPath $Path).Path

if (-not (Test-Path -LiteralPath (Join-Path $Path 'Update-7zipVersion.ps1'))) {
    throw "There is no Update-7zipVersion.ps1 in '$Path'."
}

Push-Location -LiteralPath $Path

try {
    if (git status --porcelain) {
        throw "The checkout in '$Path' has uncommitted changes. Commit or remove them first."
    }

    $baseCommit = git rev-parse HEAD
    $baseBranch = git branch --show-current

    if (-not $PSCmdlet.ShouldProcess($Path, "Reset the '$workBranch' branch to '$baseBranch' and run Update-7zipVersion.ps1 -Version $Version")) {
        return [pscustomobject] @{
            WorkBranch   = $workBranch
            BaseCommit   = $baseCommit
            Changed      = $false
            ChangedFiles = @()
        }
    }

    git switch --quiet --force-create $workBranch
    Write-Host "The '$workBranch' branch now starts at '$baseBranch' ($($baseCommit.Substring(0, 8)))."

    # Its output (including 7z's) goes to the log, so only the result object is returned
    & ./Update-7zipVersion.ps1 -Version $Version | Out-Host

    git add --all
    $changedFiles = @(git diff --cached --name-only)

    if ($changedFiles.Count -eq 0) {
        Write-Host "Running Update-7zipVersion.ps1 changed no files."
    } else {
        Write-Host "Changed files:`n$(($changedFiles | ForEach-Object { "  $_" }) -join "`n")"
    }

    [pscustomobject] @{
        WorkBranch   = $workBranch
        BaseCommit   = $baseCommit
        Changed      = $changedFiles.Count -gt 0
        ChangedFiles = $changedFiles
    }
} finally {
    Pop-Location
}
