#Requires -PSEdition Core

<#
.SYNOPSIS
    Finds the open 7-Zip update issue and pull request for a product line.

.DESCRIPTION
    Looks through the open issues and open pull requests for the hidden marker the 7-Zip update
    workflow puts in their descriptions, for example <!-- 7zip-update line=2.x version=26.06 -->.
    Returns, for the issue and the pull request separately, the number, the version it names, and
    what needs doing: create when none is open, none when it names the version, update when it
    names another version. Fails when the line has more than one marked open issue, or more than
    one marked open pull request. Needs the GitHub CLI (gh), signed in or with GH_TOKEN set.

.PARAMETER Repository
    The repository, as owner/name.

.PARAMETER Line
    The product line, 1.x or 2.x.

.PARAMETER Version
    The 7-Zip version the issue and pull request should name. Defaults to the latest release on
    GitHub.

.EXAMPLE
    ./.github/scripts/Find-7zipUpdateItem.ps1 -Line 2.x
#>
[CmdletBinding()]
param(
    [string] $Repository = 'chocolatey/choco',

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

$markerPattern = "<!--\s*7zip-update\s+line=$([regex]::Escape($Line))\s+version=(?<version>[\d.]+)\s*-->"

function Find-MarkedItem {
    param(
        [string] $Kind,
        [object[]] $Items
    )

    $found = @($Items | Where-Object { $_.body -match $markerPattern })

    if ($found.Count -gt 1) {
        throw "Found $($found.Count) open $($Kind)s for $Line (#$($found.number -join ', #')). Close all but one, then run the workflow again."
    }

    if ($found.Count -eq 0) {
        Write-Host "There is no open $Kind for $Line."
        return $null
    }

    $null = $found[0].body -match $markerPattern
    $item = [pscustomobject] @{
        Number  = $found[0].number
        Version = $Matches.version
        Base    = $found[0].base
    }

    Write-Host "Found the open $Kind #$($item.Number) for $Line, naming 7-Zip Version $($item.Version)."
    return $item
}

function Get-Action {
    param(
        [object] $Item
    )

    if (-not $Item) {
        return 'create'
    }

    if ([version] $Item.Version -eq [version] $Version) {
        return 'none'
    }

    return 'update'
}

$issues = gh api --paginate "repos/$Repository/issues?state=open&per_page=100" --jq '.[] | select(.pull_request == null) | {number, body}' | ForEach-Object { $_ | ConvertFrom-Json }
$pullRequests = gh api --paginate "repos/$Repository/pulls?state=open&per_page=100" --jq '.[] | {number, body, base: .base.ref}' | ForEach-Object { $_ | ConvertFrom-Json }

$issue = Find-MarkedItem -Kind 'issue' -Items $issues
$pullRequest = Find-MarkedItem -Kind 'pull request' -Items $pullRequests

$issueAction = Get-Action -Item $issue
$pullRequestAction = Get-Action -Item $pullRequest
$nothingToDo = $issueAction -eq 'none' -and $pullRequestAction -eq 'none'

if ($nothingToDo) {
    Write-Host "The open issue and pull request already name 7-Zip Version $Version, so nothing more is needed for $Line."
} else {
    Write-Host "Issue: $issueAction. Pull request: $pullRequestAction."
}

[pscustomobject] @{
    Line               = $Line
    IssueNumber        = $issue.Number
    IssueVersion       = $issue.Version
    IssueAction        = $issueAction
    PullRequestNumber  = $pullRequest.Number
    PullRequestVersion = $pullRequest.Version
    PullRequestBase    = $pullRequest.Base
    PullRequestAction  = $pullRequestAction
    NothingToDo        = $nothingToDo
}
