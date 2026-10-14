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
    [string]$KeycloakUrl = 'http://localhost:8080',
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
    Write-Error "Unable to get a token for '$username' from $KeycloakUrl (realm '$Realm'). Is the Aspire AppHost running and the persona defined in the realm? $($_.Exception.Message)"
    exit 1
}

$response.access_token
