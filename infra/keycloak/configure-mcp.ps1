param(
    [Parameter(Mandatory)][uri]$KeycloakUrl,
    [Parameter(Mandatory)][uri]$McpPublicUrl,
    [Parameter(Mandatory)][pscredential]$Credential,
    [string]$ChatGptRedirectUri = 'https://chatgpt.com/connector_platform_oauth_redirect',
    [string]$CodexRedirectUri = 'http://127.0.0.1:5557/callback',
    [string]$Realm = 'mail-manager'
)

$ErrorActionPreference = 'Stop'
foreach ($address in @($KeycloakUrl, $McpPublicUrl)) {
    if ($address.Scheme -ne 'https' -and -not ($address.IsLoopback -and $address.Scheme -eq 'http')) {
        throw 'HTTPS est obligatoire hors localhost.'
    }
}
if (-not $McpPublicUrl.AbsoluteUri.EndsWith('/api/mcp')) { throw 'McpPublicUrl doit terminer par /api/mcp.' }
if ($ChatGptRedirectUri -notmatch '^https://chatgpt\.com/(connector_platform_oauth_redirect|connector/oauth/[A-Za-z0-9_-]+)$') {
    throw "Copiez l'URI de retour exacte affichee par ChatGPT ; les jokers ne sont pas autorises."
}
$base = $KeycloakUrl.AbsoluteUri.TrimEnd('/')
if ($CodexRedirectUri -cnotmatch '^http://127\.0\.0\.1:([0-9]{1,5})/callback(/[A-Za-z0-9_-]+)?$' -or
    ([uri]$CodexRedirectUri).Port -lt 1024 -or ([uri]$CodexRedirectUri).Port -gt 65535) {
    throw 'CodexRedirectUri doit etre une adresse exacte http://127.0.0.1:PORT/callback (port 1024-65535), sans joker.'
}
$template = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'import/mail-manager-realm.json') | ConvertFrom-Json
$token = Invoke-RestMethod -Method Post -Uri "$base/realms/master/protocol/openid-connect/token" -Body @{
    client_id = 'admin-cli'; grant_type = 'password'; username = $Credential.UserName
    password = $Credential.GetNetworkCredential().Password
}
$headers = @{ Authorization = "Bearer $($token.access_token)" }
$admin = "$base/admin/realms/$([uri]::EscapeDataString($Realm))"
function Invoke-Kc([string]$Method, [string]$Path, $Body) {
    $arguments = @{ Method = $Method; Uri = "$admin/$Path"; Headers = $headers }
    if ($null -ne $Body) {
        $arguments.ContentType = 'application/json; charset=utf-8'
        $arguments.Body = [System.Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Body -Depth 50))
    }
    $result = Invoke-RestMethod @arguments
    if ($result -is [array]) { foreach ($entry in $result) { $entry } }
    else { $result }
}
try {
    $scopeTemplate = $template.clientScopes | Where-Object name -eq 'mailmanager'
    $scope = @(Invoke-Kc GET 'client-scopes' $null) | Where-Object name -eq 'mailmanager'
    if (-not $scope) {
        $scopeTemplate.protocolMappers | ForEach-Object { $_.config.'included.custom.audience' = $McpPublicUrl.AbsoluteUri }
        Invoke-Kc POST 'client-scopes' $scopeTemplate | Out-Null
        $scope = @(Invoke-Kc GET 'client-scopes' $null) | Where-Object name -eq 'mailmanager'
    }
    foreach ($mapper in $scopeTemplate.protocolMappers) {
        $mapper.config.'included.custom.audience' = $McpPublicUrl.AbsoluteUri
        $existing = @(Invoke-Kc GET "client-scopes/$($scope.id)/protocol-mappers/models" $null) | Where-Object name -eq $mapper.name
        if ($existing) {
            $mapper | Add-Member -NotePropertyName id -NotePropertyValue $existing.id -Force
            Invoke-Kc PUT "client-scopes/$($scope.id)/protocol-mappers/models/$($existing.id)" $mapper | Out-Null
        } else { Invoke-Kc POST "client-scopes/$($scope.id)/protocol-mappers/models" $mapper | Out-Null }
    }
    $allScopes = @(Invoke-Kc GET 'client-scopes' $null)
    $roles = @('demo', 'automation' | ForEach-Object { Invoke-Kc GET "roles/$_" $null })
    foreach ($clientTemplate in @($template.clients | Where-Object clientId -in @('mail-manager-chatgpt', 'mail-manager-claude', 'mail-manager-codex'))) {
        if ($clientTemplate.clientId -eq 'mail-manager-chatgpt') { $clientTemplate.redirectUris = @($ChatGptRedirectUri) }
        if ($clientTemplate.clientId -eq 'mail-manager-codex') { $clientTemplate.redirectUris = @($CodexRedirectUri) }
        $client = @(Invoke-Kc GET "clients?clientId=$($clientTemplate.clientId)" $null) | Where-Object clientId -eq $clientTemplate.clientId
        if ($client) {
            $clientTemplate | Add-Member -NotePropertyName id -NotePropertyValue $client.id -Force
            Invoke-Kc PUT "clients/$($client.id)" $clientTemplate | Out-Null
        } else {
            Invoke-Kc POST 'clients' $clientTemplate | Out-Null
            $client = @(Invoke-Kc GET "clients?clientId=$($clientTemplate.clientId)" $null) | Where-Object clientId -eq $clientTemplate.clientId
        }
        foreach ($scopeName in $clientTemplate.optionalClientScopes) {
            $optionalScope = $allScopes | Where-Object name -eq $scopeName
            if (-not $optionalScope) { throw "Le scope Keycloak '$scopeName' est absent." }
            Invoke-Kc PUT "clients/$($client.id)/optional-client-scopes/$($optionalScope.id)" $null | Out-Null
        }
        foreach ($scopeName in @('basic', 'roles')) {
            $defaultScope = $allScopes | Where-Object name -eq $scopeName
            if (-not $defaultScope) { throw "Le scope Keycloak standard '$scopeName' est absent. Restaurez-le avant d'utiliser le MCP." }
            Invoke-Kc PUT "clients/$($client.id)/default-client-scopes/$($defaultScope.id)" $null | Out-Null
        }
        Invoke-Kc POST "clients/$($client.id)/scope-mappings/realm" $roles | Out-Null
        if ($clientTemplate.clientId -eq 'mail-manager-codex') {
            $offlineRole = @(Invoke-Kc GET 'roles/offline_access' $null)
            Invoke-Kc POST "clients/$($client.id)/scope-mappings/realm" $offlineRole | Out-Null
        }
        Write-Host "Client configuré : $($clientTemplate.clientId)"
    }
    Write-Host "Configuration MCP terminee. Aucun role supplementaire n'est requis pour les utilisateurs."
} finally {
    $headers.Clear()
    $token = $null
}
