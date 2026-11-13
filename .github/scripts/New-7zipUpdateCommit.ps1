#Requires -PSEdition Core

<#
.SYNOPSIS
    Commits the staged 7-Zip update on the work branch.

.DESCRIPTION
    Commits what Update-7zipWorkBranch.ps1 staged, with a message in the house format: the subject
    (#<issue>) Update 7-Zip to Version <version>, a description, a Key Changes list of the changed
    files, and a Reason of the change list.

    The commit is authored by the account behind the GitHub token (GH_TOKEN, or the GitHub CLI's
    own login), with that account's GitHub no-reply address, so it matches the account that pushes
    it. Use -WhatIf to see the message and author without committing. Nothing is pushed.

.PARAMETER Path
    The checkout of the work branch, with the update staged.

.PARAMETER IssueNumber
    The issue the commit belongs to, for the subject prefix.

.PARAMETER BundledVersion
    The 7-Zip version the target branch bundled before the update.

.PARAMETER Version
    The 7-Zip version the update moves to. Defaults to the latest release on GitHub.

.EXAMPLE
    ./.github/scripts/New-7zipUpdateCommit.ps1 -Path ../choco-support -IssueNumber 3976 -BundledVersion 26.05 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Path = '.',

    [Parameter(Mandatory)]
    [int] $IssueNumber,

    [Parameter(Mandatory)]
    [string] $BundledVersion,

    [string] $Version
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if (-not $Version) {
    $Version = & "$PSScriptRoot/Get-7zipLatestVersion.ps1"
}

$Path = (Resolve-Path -LiteralPath $Path).Path
$account = gh api user --jq '{login, id}' | ConvertFrom-Json
$authorName = $account.login
$authorEmail = "$($account.id)+$($account.login)@users.noreply.github.com"

Push-Location -LiteralPath $Path

try {
    $changedFiles = @(git diff --cached --name-only)

    if ($changedFiles.Count -eq 0) {
        throw "Nothing is staged in '$Path', so there is nothing to commit."
    }

    $message = @(
        "(#$IssueNumber) Update 7-Zip to Version $Version"
        ''
        "Update the bundled 7-Zip from version $BundledVersion to $Version, using"
        'the Update-7zipVersion.ps1 script.'
        ''
        'Key Changes:'
        $changedFiles | ForEach-Object { "- Update $_" }
        ''
        'Reason of the change:'
        "- 7-Zip $Version is the latest 7-Zip release."
    ) -join "`n"

    if (-not $PSCmdlet.ShouldProcess($Path, "Commit the 7-Zip $Version update as $authorName")) {
        Write-Host "Author: $authorName <$authorEmail>`n`n$message"

        return [pscustomobject] @{
            Commit  = $null
            Subject = ($message -split "`n")[0]
            Author  = "$authorName <$authorEmail>"
        }
    }

    $message | git -c "user.name=$authorName" -c "user.email=$authorEmail" commit --quiet --file -
    $commit = git rev-parse HEAD
    Write-Host "Committed $($commit.Substring(0, 8)) as $authorName on '$(git branch --show-current)'."

    [pscustomobject] @{
        Commit  = $commit
        Subject = ($message -split "`n")[0]
        Author  = "$authorName <$authorEmail>"
    }
} finally {
    Pop-Location
}
