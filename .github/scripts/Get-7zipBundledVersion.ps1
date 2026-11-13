#Requires -PSEdition Core

<#
.SYNOPSIS
    Gets the 7-Zip version bundled on a branch of a GitHub repository.

.DESCRIPTION
    Reads tests/pester-tests/BundledApplications.Tests.ps1 from the branch through the GitHub API,
    without a checkout, and returns the version of its 7z entry. Needs the GitHub CLI (gh), signed
    in or with GH_TOKEN set.

.PARAMETER Repository
    The repository, as owner/name.

.PARAMETER Branch
    The branch, tag or commit to read the file from.

.EXAMPLE
    ./.github/scripts/Get-7zipBundledVersion.ps1 -Branch release/2.8.0
#>
[CmdletBinding()]
[OutputType([string])]
param(
    [string] $Repository = 'chocolatey/choco',

    [Parameter(Mandatory)]
    [string] $Branch
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$testFile = (gh api --header 'Accept: application/vnd.github.raw' "repos/$Repository/contents/tests/pester-tests/BundledApplications.Tests.ps1?ref=$Branch") -join "`n"

if ($testFile -notmatch "Name\s*=\s*'7z'\s*;\s*Version\s*=\s*'(?<bundled>[^']*)'") {
    throw "Unable to find the 7-Zip version in BundledApplications.Tests.ps1 on the '$Branch' branch."
}

$Matches.bundled
