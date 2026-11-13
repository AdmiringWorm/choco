#Requires -PSEdition Core

<#
.SYNOPSIS
    Finds the branch a product line's 7-Zip update should target.

.DESCRIPTION
    Picks the line's target branch: the highest hotfix/<major>.* branch, otherwise the highest
    release/<major>.* branch, otherwise the fallback branch. Only names that are exactly a version
    after the prefix count, so hotfix/2.8.0-beta or release/2.x are ignored. Then reads the 7-Zip
    version bundled on the target branch, and returns the branch, that version, and whether it is
    up to date. Needs the GitHub CLI (gh), signed in or with GH_TOKEN set.

.PARAMETER Repository
    The repository, as owner/name.

.PARAMETER Line
    The product line, 1.x or 2.x.

.PARAMETER FallbackBranch
    The branch to use when there is no hotfix or release branch for the line. Defaults to
    support/1.x for 1.x and develop for 2.x. The workflow passes the branch it runs on for 2.x.

.PARAMETER Version
    The 7-Zip version to compare with. Defaults to the latest release on GitHub.

.EXAMPLE
    ./.github/scripts/Get-7zipTargetBranch.ps1 -Line 1.x
#>
[CmdletBinding()]
param(
    [string] $Repository = 'chocolatey/choco',

    [Parameter(Mandatory)]
    [ValidateSet('1.x', '2.x')]
    [string] $Line,

    [string] $FallbackBranch,

    [string] $Version
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$major = $Line.Split('.')[0]

if (-not $FallbackBranch) {
    $FallbackBranch = if ($Line -eq '1.x') { 'support/1.x' } else { 'develop' }
}

if (-not $Version) {
    $Version = & "$PSScriptRoot/Get-7zipLatestVersion.ps1"
}

$branches = gh api --paginate "repos/$Repository/branches" --jq '.[].name'
$targetBranch = $null

foreach ($prefix in 'hotfix', 'release') {
    $candidates = @($branches | Where-Object { $_ -match "^$prefix/$major(\.\d+){1,3}$" })

    if ($candidates.Count -gt 0) {
        $targetBranch = $candidates | Sort-Object { [version] ($_ -replace '^[^/]+/') } -Descending | Select-Object -First 1
        break
    }
}

if (-not $targetBranch) {
    if ($branches -notcontains $FallbackBranch) {
        throw "There is no hotfix or release branch for $Line, and the fallback branch '$FallbackBranch' does not exist."
    }

    $targetBranch = $FallbackBranch
}

Write-Host "The target branch is '$targetBranch'."

$bundledVersion = & "$PSScriptRoot/Get-7zipBundledVersion.ps1" -Repository $Repository -Branch $targetBranch
Write-Host "The '$targetBranch' branch bundles 7-Zip Version $bundledVersion."

$upToDate = [version] $bundledVersion -ge [version] $Version

if ($upToDate) {
    Write-Host "The '$targetBranch' branch is up to date, so nothing more is needed for this line."
} else {
    Write-Host "The '$targetBranch' branch needs 7-Zip Version $Version."
}

[pscustomobject] @{
    Line           = $Line
    Branch         = $targetBranch
    BundledVersion = $bundledVersion
    UpToDate       = $upToDate
}
