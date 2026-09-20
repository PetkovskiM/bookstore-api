# Prepare ignored runtime files from existing local user secrets. Never print secret values.
[CmdletBinding()]
param([switch] $RefreshHttpsCertificate)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
    throw 'This helper requires Windows. See docs/docker-demo.md for the runtime file layout.'
}
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$localDirectory = Join-Path $repository '.local\docker'
$httpsDirectory = Join-Path $localDirectory 'https'
$oauthDirectory = Join-Path $localDirectory 'oauth'
$apiPath = Join-Path $localDirectory 'api.json'
$authPath = Join-Path $localDirectory 'auth.json'
$secretsRoot = Join-Path $env:APPDATA 'Microsoft\UserSecrets'
$encoding = New-Object System.Text.UTF8Encoding($false)

function Read-Settings([string] $path, [bool] $required = $false) {
    $values = @{}
    if (Test-Path -LiteralPath $path) {
        $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        foreach ($property in $json.PSObject.Properties) { $values[$property.Name] = $property.Value }
    }
    elseif ($required) { throw 'Complete the API and Auth user-secrets setup first. See README.md.' }
    return $values
}
function New-Password {
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] 32
        $random.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    }
    finally { $random.Dispose() }
}
function Set-Missing($values, [string] $name, $value) {
    if (!$values.ContainsKey($name)) { $values[$name] = $value }
}
function Container-Connection([string] $value) {
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'Configure both database connection strings in user secrets first.' }
    $connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($value)
    $connection['Data Source'] = 'sqlserver,1433'
    return $connection.ConnectionString
}
function Check-Certificate([string] $path, [string] $password) {
    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
        $path, $password, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    try {
        if (!$certificate.HasPrivateKey -or $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow) {
            throw 'A Docker certificate is expired or missing its private key. See docs/docker-demo.md.'
        }
    }
    finally { $certificate.Dispose() }
}

$apiSecrets = Read-Settings (Join-Path $secretsRoot 'bookstore-api-development\secrets.json') $true
$authSecrets = Read-Settings (Join-Path $secretsRoot 'bookstore-auth-development\secrets.json') $true
$apiExisted = Test-Path -LiteralPath $apiPath
$authExisted = Test-Path -LiteralPath $authPath
$api = Read-Settings $apiPath
$auth = Read-Settings $authPath
foreach ($key in @('DevelopmentDemo:ManagementClientSecret', 'DevelopmentDemo:UserPassword')) {
    if (!$authSecrets[$key]) { throw 'Complete scripts/Initialize-AuthDevelopment.ps1 first.' }
    Set-Missing $auth $key $authSecrets[$key]
}
Set-Missing $api 'ConnectionStrings:Bookstore' (Container-Connection $apiSecrets['ConnectionStrings:Bookstore'])
Set-Missing $auth 'ConnectionStrings:Authentication' (Container-Connection $authSecrets['ConnectionStrings:Authentication'])
Set-Missing $auth 'Auth:Issuer' 'https://localhost:7200/'
Set-Missing $auth 'Auth:BrowserRedirectUris:0' 'https://localhost:7200/demo/callback'
Set-Missing $auth 'Auth:BrowserRedirectUris:1' 'https://localhost:7100/swagger/oauth2-redirect.html'

$httpsPasswordKey = 'Kestrel:Certificates:Default:Password'
$httpsPassword = $api[$httpsPasswordKey]
if (!$httpsPassword) { $httpsPassword = $auth[$httpsPasswordKey] }
if (!$httpsPassword) { $httpsPassword = New-Password }
foreach ($values in @($api, $auth)) {
    Set-Missing $values $httpsPasswordKey $httpsPassword
    Set-Missing $values 'Kestrel:Certificates:Default:Path' '/https/localhost.pfx'
    if ($values[$httpsPasswordKey] -cne $httpsPassword) { throw 'The Docker API/Auth HTTPS certificate passwords do not match. Existing files were preserved.' }
}

[void][System.IO.Directory]::CreateDirectory($httpsDirectory)
[void][System.IO.Directory]::CreateDirectory($oauthDirectory)
$httpsPath = Join-Path $httpsDirectory 'localhost.pfx'
$publicPath = Join-Path $httpsDirectory 'localhost.crt'
if (!(Test-Path -LiteralPath $httpsPath) -or $RefreshHttpsCertificate) {
    if (!$RefreshHttpsCertificate -and ($apiExisted -or $authExisted)) {
        throw 'The existing Docker configuration is missing its HTTPS certificate. Review the files, then use -RefreshHttpsCertificate to export a replacement.'
    }
    $trustedThumbprints = @(Get-ChildItem Cert:\CurrentUser\Root | Select-Object -ExpandProperty Thumbprint)
    $httpsCertificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.HasPrivateKey -and $_.NotAfter.ToUniversalTime() -gt [DateTime]::UtcNow.AddDays(1) -and
        $_.NotBefore.ToUniversalTime() -le [DateTime]::UtcNow -and
        $_.Extensions.Oid.Value -contains '1.3.6.1.4.1.311.84.1.1' -and $_.Thumbprint -in $trustedThumbprints
    } | Sort-Object NotAfter -Descending | Select-Object -First 1
    if (!$httpsCertificate) { throw 'Run dotnet dev-certs https --trust as your normal Windows user, then retry this helper.' }
    [System.IO.File]::WriteAllBytes($httpsPath, $httpsCertificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $httpsPassword))
    $base64 = [Convert]::ToBase64String($httpsCertificate.RawData, [Base64FormattingOptions]::InsertLineBreaks)
    [System.IO.File]::WriteAllText($publicPath, "-----BEGIN CERTIFICATE-----`n$base64`n-----END CERTIFICATE-----`n", $encoding)
}
Check-Certificate $httpsPath $httpsPassword
if (!(Test-Path -LiteralPath $publicPath)) { throw 'The public HTTPS certificate is missing. Use -RefreshHttpsCertificate after reviewing the runtime files.' }

foreach ($purpose in @('Signing', 'Encryption')) {
    $passwordKey = 'Certificates:' + $purpose + ':Password'
    Set-Missing $auth $passwordKey (New-Password)
    $filename = $purpose.ToLowerInvariant() + '.pfx'
    Set-Missing $auth ('Certificates:' + $purpose + ':Path') ('/oauth/' + $filename)
    $path = Join-Path $oauthDirectory $filename
    if (!(Test-Path -LiteralPath $path)) {
        if ($authExisted) { throw 'An existing Docker signing/encryption certificate is missing. Restore it from backup; setup will not silently rotate keys.' }
        $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider(3072)
        $rsa.PersistKeyInCsp = $false
        $certificate = $null
        try {
            $subject = New-Object System.Security.Cryptography.X509Certificates.X500DistinguishedName(('CN=Bookstore Docker demo ' + $purpose))
            $request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest($subject, $rsa,
                [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddYears(1))
            [System.IO.File]::WriteAllBytes($path, $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $auth[$passwordKey]))
        }
        finally { if ($certificate) { $certificate.Dispose() }; $rsa.Dispose() }
    }
    Check-Certificate $path $auth[$passwordKey]
}

[System.IO.File]::WriteAllText($apiPath, ($api | ConvertTo-Json -Depth 10), $encoding)
[System.IO.File]::WriteAllText($authPath, ($auth | ConvertTo-Json -Depth 10), $encoding)
Write-Output 'Docker runtime files are ready in the ignored .local/docker directory. Existing settings and OAuth keys were preserved.'
Write-Output 'No user-secrets files, database records, or .env values were changed. No credentials were printed.'
Write-Output 'Stop local API/Auth processes, then run: docker compose --profile demo up -d --build --wait'
