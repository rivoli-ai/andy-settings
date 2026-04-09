# Andy Settings API - PowerShell Example
# Usage: .\example.ps1

$BaseUrl = if ($env:ANDY_SETTINGS_URL) { $env:ANDY_SETTINGS_URL } else { "https://localhost:5300" }
$Token = if ($env:ANDY_SETTINGS_TOKEN) { $env:ANDY_SETTINGS_TOKEN } else { "your-jwt-token" }
$Headers = @{ Authorization = "Bearer $Token"; "Content-Type" = "application/json" }

# Skip SSL for local development
if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSDefaultParameterValues['Invoke-RestMethod:SkipCertificateCheck'] = $true
} else {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
}

# 1. List definitions
Write-Host "=== List Definitions ===" -ForegroundColor Cyan
$definitions = Invoke-RestMethod -Uri "$BaseUrl/api/definitions" -Headers $Headers
$definitions | ConvertTo-Json -Depth 5 | Write-Host

# 2. Resolve effective value
Write-Host "`n=== Resolve Effective Value ===" -ForegroundColor Cyan
$resolveBody = @{
    key = "andy.containers.defaultProvider"
    context = @{
        applicationCode = "containers"
        userId = "user-123"
    }
} | ConvertTo-Json -Depth 3

$resolved = Invoke-RestMethod -Uri "$BaseUrl/api/effective/resolve" -Method POST -Headers $Headers -Body $resolveBody
$resolved | ConvertTo-Json -Depth 5 | Write-Host

# 3. Set a value
Write-Host "`n=== Set Value ===" -ForegroundColor Cyan
$setBody = @{
    definitionKey = "andy.containers.defaultProvider"
    scopeType = "User"
    scopeId = "user-123"
    valueJson = '"docker"'
} | ConvertTo-Json

Invoke-RestMethod -Uri "$BaseUrl/api/values" -Method POST -Headers $Headers -Body $setBody
Write-Host "Value set successfully"

# 4. Explain resolution
Write-Host "`n=== Explain Resolution ===" -ForegroundColor Cyan
$explanation = Invoke-RestMethod -Uri "$BaseUrl/api/effective/explain" -Method POST -Headers $Headers -Body $resolveBody
$explanation | ConvertTo-Json -Depth 5 | Write-Host

# 5. Export settings
Write-Host "`n=== Export Settings ===" -ForegroundColor Cyan
$export = Invoke-RestMethod -Uri "$BaseUrl/api/export?applicationCode=containers" -Headers $Headers
$export | ConvertTo-Json -Depth 5 | Write-Host
