<#
.SYNOPSIS
    Generates a client project from this template, non-interactively. Used by the init-project skill; usable without AI.
.EXAMPLE
    ./eng/init/new-project.ps1 -Name Contoso.Orders -Authorization AppRolesAndGroups
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$')]
    [string]$Name,
    [string]$Output,
    [ValidateSet('Authenticated', 'AppRoles', 'Groups', 'AppRolesAndGroups')]
    [string]$Authorization = 'AppRoles',
    [bool]$Aspire = $true,
    [bool]$RateLimiting = $true,
    [bool]$Tests = $true,
    [string]$Remote
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("new-project: '$FilePath $($Arguments -join ' ')' failed with exit code $LASTEXITCODE")
        exit 1
    }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $Output) { $Output = Join-Path (Split-Path $root -Parent) $Name }
if ((Test-Path $Output) -and (Get-ChildItem -Force $Output | Select-Object -First 1)) {
    [Console]::Error.WriteLine("new-project: '$Output' already exists and is not empty; nothing was changed")
    exit 1
}

$toFlag = { param([bool]$value) $value.ToString().ToLowerInvariant() }

Invoke-Checked 'dotnet' @('new', 'install', $root, '--force')
Invoke-Checked 'dotnet' @(
    'new', 'templatewebcqrs',
    '--name', $Name,
    '--output', $Output,
    '--AuthorizationModel', $Authorization,
    '--EnableAspire', (& $toFlag $Aspire),
    '--EnableRateLimiting', (& $toFlag $RateLimiting),
    '--EnableTests', (& $toFlag $Tests))
Invoke-Checked 'git' @('-C', $Output, 'init', '--quiet')
if ($Remote) { Invoke-Checked 'git' @('-C', $Output, 'remote', 'add', 'origin', $Remote) }

Write-Output "new-project: created $Output"
