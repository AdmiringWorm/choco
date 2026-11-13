#Requires -PSEdition Core

<#
.SYNOPSIS
    Gets the version of the latest 7-Zip release on GitHub.

.DESCRIPTION
    Reads the latest release of the ip7z/7zip repository through the GitHub API, and returns its
    tag, for example 26.06. When the GH_TOKEN or GITHUB_TOKEN environment variable is set, the
    token is sent with the request to avoid the lower rate limit for anonymous requests.

.EXAMPLE
    ./.github/scripts/Get-7zipLatestVersion.ps1
#>
[CmdletBinding()]
[OutputType([string])]
param()

$ErrorActionPreference = 'Stop'

$headers = @{}
$token = if ($env:GH_TOKEN) { $env:GH_TOKEN } else { $env:GITHUB_TOKEN }

if ($token) {
    $headers.Authorization = "Bearer $token"
}

$latestRelease = Invoke-RestMethod -Uri 'https://api.github.com/repos/ip7z/7zip/releases/latest' -Headers $headers
$latestRelease.tag_name
