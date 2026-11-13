#Requires -PSEdition Core

<#
.SYNOPSIS
    Creates or updates the 7-Zip update issue for a product line.

.DESCRIPTION
    Without -IssueNumber, creates the line's issue, with the Dependency Change label and the
    milestone that matches the target branch: the version for a hotfix or release branch (for
    example 2.8.0 for release/2.8.0), otherwise the line (1.x or 2.x). A label or milestone that
    does not exist in the repository is skipped with a warning.

    With -IssueNumber, rewrites that issue's title and description for the new version, and adds a
    comment saying which version it moved from. Labels and milestone are left alone.

    The description carries the hidden marker Find-7zipUpdateItem.ps1 looks for. Use -WhatIf to see
    the title and description without changing anything. Needs the GitHub CLI (gh), signed in or
    with GH_TOKEN set to a token that can write issues.

.PARAMETER Repository
    The repository, as owner/name.

.PARAMETER Line
    The product line, 1.x or 2.x.

.PARAMETER TargetBranch
    The branch the update targets, from Get-7zipTargetBranch.ps1.

.PARAMETER BundledVersion
    The 7-Zip version bundled on the target branch today.

.PARAMETER Version
    The 7-Zip version to update to. Defaults to the latest release on GitHub.

.PARAMETER IssueNumber
    The open issue to update. Leave it out to create a new issue.

.PARAMETER PreviousVersion
    The version the open issue names today, for the comment added when it is updated.

.EXAMPLE
    ./.github/scripts/Set-7zipUpdateIssue.ps1 -Line 2.x -TargetBranch develop -BundledVersion 26.05 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Repository = 'chocolatey/choco',

    [Parameter(Mandatory)]
    [ValidateSet('1.x', '2.x')]
    [string] $Line,

    [Parameter(Mandatory)]
    [string] $TargetBranch,

    [Parameter(Mandatory)]
    [string] $BundledVersion,

    [string] $Version,

    [int] $IssueNumber,

    [string] $PreviousVersion
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if (-not $Version) {
    $Version = & "$PSScriptRoot/Get-7zipLatestVersion.ps1"
}

$title = "Update the bundled version of 7-Zip to $Version"

$creditsCriteria = if ($Line -eq '1.x') {
    '- [ ] `docs/legal/CREDITS.md` lists the new version and its license text.'
} else {
    '- [ ] `docs/legal/CREDITS.json` lists the new version, and `CREDITS.md` and `CREDITS.pdf` are regenerated with the Credits Generator.'
}

$body = @(
    '### Is Your Feature Request Related To A Problem? Please describe.'
    ''
    "Chocolatey CLI $Line bundles 7-Zip $BundledVersion on the ``$TargetBranch`` branch."
    ''
    '### Describe The Solution. Why is it needed?'
    ''
    "Update the bundled 7-Zip to [$Version](https://github.com/ip7z/7zip/releases/tag/$Version), the latest 7-Zip release, so Chocolatey CLI includes its fixes and improvements."
    ''
    '### User Story'
    ''
    '_No response_'
    ''
    '### Additional Context'
    ''
    'The scheduled Update 7-Zip workflow opened this issue, and opens the pull request with the update.'
    ''
    '### Acceptance Criteria'
    ''
    "- [ ] The bundled 7-Zip (``7z.exe`` and ``7z.dll``) is updated to $Version."
    $creditsCriteria
    "- [ ] ``tests/pester-tests/BundledApplications.Tests.ps1`` expects 7-Zip $Version."
    ''
    '### Related Issues'
    ''
    '_No response_'
    ''
    "<!-- 7zip-update line=$Line version=$Version -->"
) -join "`n"

if ($IssueNumber) {
    $comment = if ($PreviousVersion) {
        "7-Zip $Version was released, so this issue now covers $Version instead of $PreviousVersion."
    } else {
        "7-Zip $Version was released, so this issue now covers $Version."
    }

    if ($PSCmdlet.ShouldProcess("$Repository#$IssueNumber", "Update the issue to 7-Zip $Version")) {
        $body | gh issue edit $IssueNumber --repo $Repository --title $title --body-file - | Out-Null
        gh issue comment $IssueNumber --repo $Repository --body $comment | Out-Null
        Write-Host "Updated issue #$IssueNumber to 7-Zip Version $Version."
    } else {
        Write-Host "Title: $title`n`n$body`n`nComment: $comment"
    }

    return [pscustomobject] @{
        Number = $IssueNumber
        Action = 'updated'
    }
}

$milestone = if ($TargetBranch -match '^(hotfix|release)/(?<version>\d+(\.\d+)+)$') { $Matches.version } else { $Line }
$label = 'Dependency Change'

$arguments = @('--repo', $Repository, '--title', $title, '--body-file', '-')
$labels = gh api --paginate "repos/$Repository/labels?per_page=100" --jq '.[].name'
$milestones = gh api --paginate "repos/$Repository/milestones?state=open&per_page=100" --jq '.[].title'

if ($labels -contains $label) {
    $arguments += '--label', $label
} else {
    Write-Warning "The '$label' label does not exist in $Repository, so the issue is created without it."
    $label = '(none)'
}

if ($milestones -contains $milestone) {
    $arguments += '--milestone', $milestone
} else {
    Write-Warning "The '$milestone' milestone does not exist in $Repository, so the issue is created without one."
    $milestone = '(none)'
}

if (-not $PSCmdlet.ShouldProcess($Repository, "Create the $Line issue for 7-Zip $Version")) {
    Write-Host "Title: $title`nLabel: $label`nMilestone: $milestone`n`n$body"

    return [pscustomobject] @{
        Number = $null
        Action = 'created'
    }
}

$url = $body | gh issue create @arguments
$number = [int] ($url -split '/')[-1]
Write-Host "Created issue #$number for 7-Zip Version $Version ($url)."

[pscustomobject] @{
    Number = $number
    Action = 'created'
}
