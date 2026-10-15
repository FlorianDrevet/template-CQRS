<#
.SYNOPSIS
    Local smoke test of the Aspire stack: gets tokens for two personas and checks the expected HTTP status codes.
.EXAMPLE
    ./scripts/smoke-test.ps1
#>
param(
    [string]$ApiBaseUrl = 'https://localhost:7246',
    [string]$Admin = 'admin',
    [string]$User = 'user'
)

$ErrorActionPreference = 'Stop'
$failures = 0

function Get-Status([string]$Uri, [string]$Token) {
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    try {
        return [int](Invoke-WebRequest -Uri $Uri -Headers $headers -UseBasicParsing -Method Get).StatusCode
    }
    catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

function Test-Expected([string]$Description, [int]$Expected, [int]$Actual) {
    if ($Actual -eq $Expected) {
        Write-Output "OK   $Description ($Actual)"
    }
    else {
        Write-Output "FAIL ${Description}: expected $Expected, got $Actual"
        $script:failures++
    }
}

$adminToken = & "$PSScriptRoot/get-token.ps1" -Persona $Admin
$userToken = & "$PSScriptRoot/get-token.ps1" -Persona $User

Test-Expected 'GET /me without token' 401 (Get-Status "$ApiBaseUrl/me" $null)
Test-Expected "GET /me as $Admin" 200 (Get-Status "$ApiBaseUrl/me" $adminToken)
Test-Expected "GET /me as $User" 200 (Get-Status "$ApiBaseUrl/me" $userToken)

$pingAnonymous = Get-Status "$ApiBaseUrl/admin/ping" $null
if ($pingAnonymous -eq 404) {
    Write-Output 'SKIP /admin/ping (not generated for this authorization model)'
}
else {
    Test-Expected 'GET /admin/ping without token' 401 $pingAnonymous
    Test-Expected "GET /admin/ping as $Admin" 200 (Get-Status "$ApiBaseUrl/admin/ping" $adminToken)
    Test-Expected "GET /admin/ping as $User" 403 (Get-Status "$ApiBaseUrl/admin/ping" $userToken)
}

if ($failures -gt 0) {
    [Console]::Error.WriteLine("smoke-test: $failures check(s) failed")
    exit 1
}
Write-Output 'smoke-test: all checks passed'
