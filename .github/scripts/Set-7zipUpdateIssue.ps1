#Requires -PSEdition Core

<#
.SYNOPSIS
    Creates or updates the 7-Zip update issue for a product line.

.DESCRIPTION
    Without -IssueNumber, creates the line's issue, with the Dependency Change label and the
    milestone that matches the target branch: the version for a hotfix or release branch (for
    example 2.8.0 for release/2.8.0), otherwise the line (1.x or 2.x). A label or milestone that
    does not exist in the repository is skipped with a warning.

    With -IssueNumber, moves that issue to the new version: a new title, the previous version
    replaced by the new one in its description (except in the problem section, which describes the
    situation when the issue was opened), the marker moved to the new version, and a comment saying
    which version it moved from. The rest of the description, including any edits, is kept. It also
    adds the Dependency Change label when the
    issue does not have it, and sets the milestone when the issue has none, so a milestone a
    maintainer has set is kept.

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
    The version the open issue names today. Needed with -IssueNumber: it is the version replaced in
    the description, and named in the comment.

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

$label = 'Dependency Change'
$milestone = if ($TargetBranch -match '^(hotfix|release)/(?<version>\d+(\.\d+)+)$') { $Matches.version } else { $Line }

$labels = gh api --paginate "repos/$Repository/labels?per_page=100" --jq '.[].name'
$milestones = gh api --paginate "repos/$Repository/milestones?state=open&per_page=100" --jq '.[].title'

if ($labels -notcontains $label) {
    Write-Warning "The '$label' label does not exist in $Repository, so it is not added to the issue."
    $label = $null
}

if ($milestones -notcontains $milestone) {
    Write-Warning "The '$milestone' milestone does not exist in $Repository, so it is not set on the issue."
    $milestone = $null
}

if ($IssueNumber) {
    if (-not $PreviousVersion) {
        throw '-PreviousVersion is needed with -IssueNumber, to know which version to replace in the issue description.'
    }

    $comment = "7-Zip $Version was released, so this issue now covers $Version instead of $PreviousVersion."
    $current = gh issue view $IssueNumber --repo $Repository --json body,labels,milestone | ConvertFrom-Json

    # Keep the description as it is, so the problem statement and any edits stay. Only the previous
    # version is replaced, outside the problem section, and the marker moves to the new version.
    $problemHeading = '### Is Your Feature Request Related To A Problem?'
    $previousVersionPattern = "(?<!\d|\d\.)$([regex]::Escape($PreviousVersion))(?!\d|\.\d)"
    $sections = [regex]::Split($current.body, '(?m)^(?=### )')

    $body = ($sections | ForEach-Object {
        if ($_.StartsWith($problemHeading)) {
            $_
        } else {
            $_ -replace $previousVersionPattern, $Version
        }
    }) -join ''

    $body = $body -replace '<!--\s*7zip-update\s+line=\S+\s+version=[\d.]+\s*-->', "<!-- 7zip-update line=$Line version=$Version -->"

    # Only add what is missing, so a milestone a maintainer has set is kept
    $arguments = @($IssueNumber, '--repo', $Repository, '--title', $title, '--body-file', '-')

    if ($label -and $current.labels.name -notcontains $label) {
        $arguments += '--add-label', $label
    } else {
        $label = $null
    }

    if ($milestone -and -not $current.milestone) {
        $arguments += '--milestone', $milestone
    } else {
        $milestone = $null
    }

    if ($PSCmdlet.ShouldProcess("$Repository#$IssueNumber", "Update the issue to 7-Zip $Version")) {
        $body | gh issue edit @arguments | Out-Null
        gh issue comment $IssueNumber --repo $Repository --body $comment | Out-Null
        Write-Host "Updated issue #$IssueNumber to 7-Zip Version $Version."
    } else {
        Write-Host "Title: $title`nAdds label: $(if ($label) { $label } else { '(none)' })`nSets milestone: $(if ($milestone) { $milestone } else { '(none)' })`n`n$body`n`nComment: $comment"
    }

    return [pscustomobject] @{
        Number = $IssueNumber
        Action = 'updated'
    }
}

$arguments = @('--repo', $Repository, '--title', $title, '--body-file', '-')

if ($label) {
    $arguments += '--label', $label
}

if ($milestone) {
    $arguments += '--milestone', $milestone
}

if (-not $PSCmdlet.ShouldProcess($Repository, "Create the $Line issue for 7-Zip $Version")) {
    Write-Host "Title: $title`nLabel: $(if ($label) { $label } else { '(none)' })`nMilestone: $(if ($milestone) { $milestone } else { '(none)' })`n`n$body"

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
