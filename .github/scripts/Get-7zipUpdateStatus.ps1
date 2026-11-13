#Requires -PSEdition Core

<#
.SYNOPSIS
    Checks whether a branch bundles the latest 7-Zip release.

.DESCRIPTION
    Compares the latest 7-Zip release on GitHub with the 7-Zip version bundled on the branch, and
    returns both versions and whether an update is needed. Refuses the 1.x branches (support/1.x,
    hotfix/1* and release/1*), because the 7-Zip update workflow only runs from 2.x related
    branches. Needs the GitHub CLI (gh), signed in or with GH_TOKEN set.

.PARAMETER Repository
    The repository, as owner/name.

.PARAMETER Branch
    The branch to check. The workflow passes the branch it runs on.

.EXAMPLE
    ./.github/scripts/Get-7zipUpdateStatus.ps1 -Branch develop
#>
[CmdletBinding()]
param(
    [string] $Repository = 'chocolatey/choco',

    [string] $Branch = 'develop'
)

$ErrorActionPreference = 'Stop'

if ($Branch -match '^(support/1\.x$|hotfix/1|release/1)') {
    throw "The workflow cannot run from the '$Branch' branch. Run it from develop or another 2.x branch instead."
}

$latestVersion = & "$PSScriptRoot/Get-7zipLatestVersion.ps1"
Write-Host "The latest 7-Zip release on GitHub is Version $latestVersion."

$bundledVersion = & "$PSScriptRoot/Get-7zipBundledVersion.ps1" -Repository $Repository -Branch $Branch
Write-Host "The '$Branch' branch bundles 7-Zip Version $bundledVersion."

$updateNeeded = [version] $latestVersion -gt [version] $bundledVersion

if ($updateNeeded) {
    Write-Host "7-Zip Version $latestVersion is newer, so the bundled 7-Zip needs updating."
} else {
    Write-Host "The bundled 7-Zip is up to date."
}

[pscustomobject] @{
    Version        = $latestVersion
    BundledVersion = $bundledVersion
    UpdateNeeded   = $updateNeeded
}
