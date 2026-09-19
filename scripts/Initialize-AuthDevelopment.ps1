# Run after configuring Bookstore.Api user secrets. Existing Auth values are preserved.
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
    throw 'This helper is for Windows. Configure Bookstore.Auth user secrets manually on other systems.'
}
$secretsRoot = Join-Path $env:APPDATA 'Microsoft\UserSecrets'
$apiPath = Join-Path $secretsRoot 'bookstore-api-development\secrets.json'
$authDirectory = Join-Path $secretsRoot 'bookstore-auth-development'
$authPath = Join-Path $authDirectory 'secrets.json'
$values = @{}
if (Test-Path -LiteralPath $authPath) {
    $existing = Get-Content -LiteralPath $authPath -Raw | ConvertFrom-Json
    foreach ($property in $existing.PSObject.Properties) { $values[$property.Name] = $property.Value }
}

if (!$values['ConnectionStrings:Authentication']) {
    $api = Get-Content -LiteralPath $apiPath -Raw | ConvertFrom-Json
    if (!$api.'ConnectionStrings:Bookstore') { throw 'Configure Bookstore.Api user secrets first. See README.md.' }
    $connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($api.'ConnectionStrings:Bookstore')
    $connection['Initial Catalog'] = 'BookstoreAuth'
    $values['ConnectionStrings:Authentication'] = $connection.ConnectionString
}

$random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    foreach ($key in @('DevelopmentDemo:ManagementClientSecret', 'DevelopmentDemo:UserPassword')) {
        if (!$values[$key]) {
            $bytes = New-Object byte[] 32
            $random.GetBytes($bytes)
            $values[$key] = 'Aa1!' + [Convert]::ToBase64String($bytes)
        }
    }
}
finally { $random.Dispose() }

[void][System.IO.Directory]::CreateDirectory($authDirectory)
[System.IO.File]::WriteAllText($authPath, ($values | ConvertTo-Json -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'Auth development settings are ready. Existing values were preserved.'
Write-Output 'View the generated credentials with Bookstore.Auth > Manage User Secrets in Visual Studio.'
Write-Output 'Demo username: demo@bookstore.local. No credentials were printed.'
