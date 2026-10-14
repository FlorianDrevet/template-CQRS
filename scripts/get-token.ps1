<#
.SYNOPSIS
    Prints an access token for a local Keycloak persona (local development only).
.EXAMPLE
    ./scripts/get-token.ps1 -Persona admin
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Persona,
    [string]$Password = 'Passw0rd!',
    [string]$KeycloakUrl = 'https://localhost:8080',
    [string]$Realm = 'template',
    [string]$ClientId = 'template-cli'
)

$ErrorActionPreference = 'Stop'
$username = if ($Persona.Contains('@')) { $Persona } else { "$Persona@local.test" }

try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "$KeycloakUrl/realms/$Realm/protocol/openid-connect/token" `
        -ContentType 'application/x-www-form-urlencoded' `
        -Body @{
            grant_type = 'password'
            client_id  = $ClientId
            username   = $username
            password   = $Password
            scope      = 'openid'
        }
}
catch {
    $detail = $_.Exception.Message
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $detail = "$detail $($_.ErrorDetails.Message)" }
    [Console]::Error.WriteLine("get-token: unable to get a token for '$username' from $KeycloakUrl (realm '$Realm'). Is the Aspire AppHost running and the persona defined in the realm? $detail")
    exit 1
}

if (-not $response.access_token) {
    [Console]::Error.WriteLine("get-token: no access_token in Keycloak response from $KeycloakUrl (realm '$Realm').")
    exit 1
}

$response.access_token
