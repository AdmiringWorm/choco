#Requires -PSEdition Core

<#
.SYNOPSIS
    Pushes the 7-Zip update work branch, and creates or updates its pull request.

.DESCRIPTION
    Force-pushes the checked-out work branch (update-7zip-<line>) to origin, because it is rebuilt
    from the target branch every time. Then, without -PullRequestNumber, creates a pull request
    from it into the target branch. With -PullRequestNumber, moves that pull request to the new
    version: new title and description, its base set to the target branch, and a comment saying
    which version it moved from.

    The description follows the Chocolatey pull request template, lists the files changed in the
    work branch's last commit, closes the issue with Fixes #<issue>, and carries the hidden marker
    Find-7zipUpdateItem.ps1 looks for. On 2.x, when CREDITS.json changed, it also asks for
    CREDITS.md and CREDITS.pdf to be regenerated with the Credits Generator.

    Use -WhatIf to see the push and the pull request without changing anything. Needs the GitHub
    CLI (gh), signed in or with GH_TOKEN set to a token that can push and write pull requests.

.PARAMETER Repository
    The repository, as owner/name.

.PARAMETER Path
    The checkout of the work branch, with the update committed.

.PARAMETER Line
    The product line, 1.x or 2.x.

.PARAMETER TargetBranch
    The branch the pull request merges into, from Get-7zipTargetBranch.ps1.

.PARAMETER IssueNumber
    The issue the pull request fixes.

.PARAMETER Version
    The 7-Zip version the update moves to. Defaults to the latest release on GitHub.

.PARAMETER PullRequestNumber
    The open pull request to update. Leave it out to create a new pull request.

.PARAMETER PreviousVersion
    The version the open pull request names today, for the comment added when it is updated.

.EXAMPLE
    ./.github/scripts/Set-7zipUpdatePullRequest.ps1 -Path ../choco-support -Line 1.x -TargetBranch support/1.x -IssueNumber 3976 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Repository = 'chocolatey/choco',

    [string] $Path = '.',

    [Parameter(Mandatory)]
    [ValidateSet('1.x', '2.x')]
    [string] $Line,

    [Parameter(Mandatory)]
    [string] $TargetBranch,

    [Parameter(Mandatory)]
    [int] $IssueNumber,

    [string] $Version,

    [int] $PullRequestNumber,

    [string] $PreviousVersion
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if (-not $Version) {
    $Version = & "$PSScriptRoot/Get-7zipLatestVersion.ps1"
}

if ($PullRequestNumber -and -not $PreviousVersion) {
    throw '-PreviousVersion is needed with -PullRequestNumber, for the comment added to the pull request.'
}

$workBranch = "update-7zip-$Line"
$Path = (Resolve-Path -LiteralPath $Path).Path

Push-Location -LiteralPath $Path

try {
    $currentBranch = git branch --show-current

    if ($currentBranch -ne $workBranch) {
        throw "The checkout in '$Path' is on '$currentBranch', not on the work branch '$workBranch'."
    }

    $changedFiles = @(git show --name-only --format= HEAD | Where-Object { $_ })
} finally {
    Pop-Location
}

$title = "(#$IssueNumber) Update 7-Zip to Version $Version"
$changeList = $changedFiles | ForEach-Object { "- ``$_``" }

$creditsNote = @()

if ($changedFiles -contains 'docs/legal/CREDITS.json') {
    $creditsNote = @(
        ''
        '**Before merging:** `CREDITS.md` and `CREDITS.pdf` are generated from `CREDITS.json` by the Credits Generator, so they still list the previous 7-Zip version. Regenerate them and add them to this pull request.'
    )
}

$body = @(
    '## Description Of Changes'
    ''
    "Updates the 7-Zip files bundled with Chocolatey CLI $Line to version $Version, by running ``Update-7zipVersion.ps1`` on ``$TargetBranch``. Changed files:"
    ''
    $changeList
    $creditsNote
    ''
    '## Motivation and Context'
    ''
    "7-Zip [$Version](https://github.com/ip7z/7zip/releases/tag/$Version) is the latest 7-Zip release. The scheduled Update 7-Zip workflow opened this pull request."
    ''
    '## Testing'
    ''
    'N/A: Opened automatically by the Update 7-Zip workflow, which does not test the change. The build and test workflows run on this pull request.'
    ''
    '### Operating Systems Testing'
    ''
    'N/A: Opened automatically, and not tested on any operating system yet.'
    ''
    '## Change Types Made'
    ''
    '* [ ] Bug fix (non-breaking change).'
    '* [x] Feature / Enhancement (non-breaking change).'
    '* [ ] Breaking change (fix or feature that could cause existing functionality to change).'
    '* [x] Documentation changes.'
    '* [ ] PowerShell code changes.'
    ''
    '## Change Checklist'
    ''
    '* [x] Requires a change to the documentation.'
    '* [x] Documentation has been updated.'
    '* [ ] Tests to cover my changes, have been added.'
    '* [ ] All new and existing tests passed?'
    '* [ ] PowerShell code changes: PowerShell v3 compatibility checked?'
    '* [ ] Significant design decisions: [ADR](https://github.com/chocolatey/choco/blob/master/CONTRIBUTING.md#architecture-decision-records-adrs) has been created by a Chocolatey team member?'
    '* [x] All items are complete on the [Definition of Done](https://github.com/chocolatey/home/blob/main/definition-of-done.md).'
    ''
    '## Related Issue'
    ''
    "Fixes #$IssueNumber"
    ''
    "<!-- 7zip-update line=$Line version=$Version -->"
) -join "`n"

# TODO: Request a review from the people who maintain Chocolatey CLI, once it is decided who that
# is (named maintainers or a team), for example with --reviewer on gh pr create and --add-reviewer
# on gh pr edit.

if ($PSCmdlet.ShouldProcess("$Repository $workBranch", 'Force-push the work branch')) {
    Push-Location -LiteralPath $Path

    try {
        git push --force origin "HEAD:refs/heads/$workBranch" 2>&1 | Out-Host
        Write-Host "Pushed '$workBranch' to origin."
    } finally {
        Pop-Location
    }
}

if ($PullRequestNumber) {
    $comment = "7-Zip $Version was released, so this pull request now updates to $Version instead of $PreviousVersion. The '$workBranch' branch was rebuilt from '$TargetBranch'."

    if (-not $PSCmdlet.ShouldProcess("$Repository#$PullRequestNumber", "Update the pull request to 7-Zip $Version")) {
        Write-Host "Title: $title`nBase: $TargetBranch`n`n$body`n`nComment: $comment"

        return [pscustomobject] @{
            Number = $PullRequestNumber
            Action = 'updated'
        }
    }

    $body | gh pr edit $PullRequestNumber --repo $Repository --title $title --base $TargetBranch --body-file - | Out-Null
    gh pr comment $PullRequestNumber --repo $Repository --body $comment | Out-Null
    Write-Host "Updated pull request #$PullRequestNumber to 7-Zip Version $Version."

    return [pscustomobject] @{
        Number = $PullRequestNumber
        Action = 'updated'
    }
}

if (-not $PSCmdlet.ShouldProcess($Repository, "Create the $Line pull request for 7-Zip $Version into '$TargetBranch'")) {
    Write-Host "Title: $title`nBase: $TargetBranch`nHead: $workBranch`n`n$body"

    return [pscustomobject] @{
        Number = $null
        Action = 'created'
    }
}

$url = $body | gh pr create --repo $Repository --base $TargetBranch --head $workBranch --title $title --body-file -
$number = [int] ($url -split '/')[-1]
Write-Host "Created pull request #$number for 7-Zip Version $Version ($url)."

[pscustomobject] @{
    Number = $number
    Action = 'created'
}
