# Prepares the ignored Docker runtime files without requiring a host .NET SDK.
# Existing valid runtime settings are validated and preserved; this script never rotates them.
[CmdletBinding()]
param(
    [switch] $TrustHttpsCertificate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'This helper requires Windows PowerShell. See README.md for the Docker quick start.'
}

foreach ($commandName in @('docker', 'New-SelfSignedCertificate', 'Export-PfxCertificate')) {
    if ($null -eq (Get-Command -Name $commandName -ErrorAction SilentlyContinue)) {
        throw "Required command '$commandName' was not found. Install/start Docker Desktop and use Windows PowerShell, then retry."
    }
}

$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$environmentPath = Join-Path $repository '.env'
$localRoot = Join-Path $repository '.local'
$dockerRoot = Join-Path $localRoot 'docker'
$apiPath = Join-Path $dockerRoot 'api.json'
$authPath = Join-Path $dockerRoot 'auth.json'
$httpsDirectory = Join-Path $dockerRoot 'https'
$oauthDirectory = Join-Path $dockerRoot 'oauth'
$httpsPfxPath = Join-Path $httpsDirectory 'localhost.pfx'
$httpsCertificatePath = Join-Path $httpsDirectory 'localhost.crt'
$signingPfxPath = Join-Path $oauthDirectory 'signing.pfx'
$encryptionPfxPath = Join-Path $oauthDirectory 'encryption.pfx'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-Utf8File {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $Content)

    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Get-SettingValue {
    param([Parameter(Mandatory = $true)]$Settings, [Parameter(Mandatory = $true)][string] $Name)

    $property = $Settings.PSObject.Properties | Where-Object { $_.Name -ceq $Name } | Select-Object -First 1
    if ($null -eq $property) { return $null }
    return [string] $property.Value
}

function Assert-NonPlaceholderSecret {
    param([AllowNull()][string] $Value, [Parameter(Mandatory = $true)][string] $Description)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -lt 12 -or
        $Value -match '^(?i)(change|replace|example|placeholder|secret|password|todo)') {
        throw "$Description is blank, too short, or a placeholder. Restore the intended ignored configuration; this script will not replace it."
    }
}

function Test-SqlPassword {
    param([AllowNull()][string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -lt 12 -or $Value -match '\s' -or
        $Value -match '^(?i)(change|replace|example|placeholder|secret|password|todo)') {
        return $false
    }

    $categories = 0
    if ($Value -match '[A-Z]') { $categories++ }
    if ($Value -match '[a-z]') { $categories++ }
    if ($Value -match '[0-9]') { $categories++ }
    if ($Value -match '[^A-Za-z0-9]') { $categories++ }
    return $categories -ge 3
}

function Get-RandomIndex {
    param([Parameter(Mandatory = $true)][int] $UpperBound)

    $bytes = New-Object byte[] 4
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($bytes)
        return [int] ([BitConverter]::ToUInt32($bytes, 0) % [uint32] $UpperBound)
    }
    finally {
        $random.Dispose()
    }
}

function New-RandomPassword {
    param([int] $Length = 32)

    $lower = 'abcdefghijkmnopqrstuvwxyz'
    $upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
    $digits = '23456789'
    $symbols = '!%+,-._@^'
    $all = $lower + $upper + $digits + $symbols
    $characters = New-Object System.Collections.Generic.List[char]
    foreach ($set in @($lower, $upper, $digits, $symbols)) {
        $characters.Add($set[(Get-RandomIndex $set.Length)])
    }
    while ($characters.Count -lt $Length) {
        $characters.Add($all[(Get-RandomIndex $all.Length)])
    }
    for ($index = $characters.Count - 1; $index -gt 0; $index--) {
        $otherIndex = Get-RandomIndex ($index + 1)
        $temporary = $characters[$index]
        $characters[$index] = $characters[$otherIndex]
        $characters[$otherIndex] = $temporary
    }
    return -join $characters
}

function Read-DockerEnvironment {
    param([Parameter(Mandatory = $true)][string] $Path)

    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) { continue }
        if ($line -notmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
            throw 'The existing .env is malformed. It must contain MSSQL_SA_PASSWORD and MSSQL_PORT only.'
        }
        $name = $matches[1]
        $value = $matches[2]
        if ($name -notin @('MSSQL_SA_PASSWORD', 'MSSQL_PORT') -or $values.ContainsKey($name)) {
            throw 'The existing .env contains unsupported or duplicate settings. Keep only MSSQL_SA_PASSWORD and MSSQL_PORT for the Docker demo.'
        }
        if (($value.StartsWith("'") -and $value.EndsWith("'")) -or ($value.StartsWith('"') -and $value.EndsWith('"'))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        elseif ($value.StartsWith("'") -or $value.EndsWith("'") -or $value.StartsWith('"') -or $value.EndsWith('"')) {
            throw "The existing .env value for $name has an unmatched quote. Correct it without changing any other Docker demo files."
        }
        if ($value -ne $value.Trim()) {
            throw "The existing .env value for $name has surrounding whitespace. Correct it without changing any other Docker demo files."
        }
        $values[$name] = $value
    }
    if ($values.Count -ne 2 -or -not $values.ContainsKey('MSSQL_SA_PASSWORD') -or -not $values.ContainsKey('MSSQL_PORT')) {
        throw 'The existing .env must contain both MSSQL_SA_PASSWORD and MSSQL_PORT. This script will not overwrite it.'
    }
    if (-not (Test-SqlPassword $values['MSSQL_SA_PASSWORD'])) {
        throw 'MSSQL_SA_PASSWORD is blank, weak, or a placeholder. Restore a strong existing value; this script will not rotate it.'
    }
    $port = 0
    if (-not [int]::TryParse($values['MSSQL_PORT'], [ref] $port) -or $port -lt 1024 -or $port -gt 65535) {
        throw 'MSSQL_PORT must be a host port from 1024 through 65535. This script will not overwrite it.'
    }
    return $values
}

function Get-DockerDemoVolumesExist {
    $volumeNames = @(& docker volume ls --format '{{.Name}}' 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop must be running with Linux containers before initializing the Docker demo.'
    }
    return (($volumeNames -contains 'bookstore_sqlserver-data') -or ($volumeNames -contains 'bookstore_auth-data-protection'))
}

function Get-JsonSettings {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $Description)

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "$Description is not valid JSON. Restore the ignored file from its local backup; this script will not overwrite it."
    }
}

function Get-ValidPfxCertificate {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Password,
        [Parameter(Mandatory = $true)][string] $Description
    )

    try {
        $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
            $Path, $Password, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    }
    catch {
        throw "$Description cannot be opened with its configured password. Restore matching ignored runtime files; this script will not rotate certificates."
    }
    if (-not $certificate.HasPrivateKey -or $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow -or
        $certificate.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow) {
        $certificate.Dispose()
        throw "$Description is expired, not yet valid, or lacks a private key. Restore it from backup; this script will not rotate certificates."
    }
    return $certificate
}

function Assert-ContainerConnection {
    param(
        [Parameter(Mandatory = $true)][string] $ConnectionString,
        [Parameter(Mandatory = $true)][string] $DatabaseName,
        [Parameter(Mandatory = $true)][string] $SqlPassword,
        [Parameter(Mandatory = $true)][string] $Description
    )

    try {
        $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($ConnectionString)
    }
    catch {
        throw "$Description is malformed. Restore matching ignored Docker configuration; this script will not overwrite it."
    }
    if ($builder.DataSource -ine 'sqlserver,1433' -or $builder.InitialCatalog -ine $DatabaseName -or
        $builder.UserID -ine 'sa' -or $builder.Password -cne $SqlPassword -or -not $builder.Encrypt -or
        -not $builder.TrustServerCertificate) {
        throw "$Description does not match the Docker SQL Server settings in .env. Restore matching ignored files; this script will not rotate credentials."
    }
}

function Assert-ExistingDockerRuntime {
    param([Parameter(Mandatory = $true)][hashtable] $EnvironmentValues)

    $api = Get-JsonSettings $apiPath 'Existing .local/docker/api.json'
    $auth = Get-JsonSettings $authPath 'Existing .local/docker/auth.json'

    Assert-ContainerConnection (Get-SettingValue $api 'ConnectionStrings:Bookstore') 'Bookstore' $EnvironmentValues['MSSQL_SA_PASSWORD'] 'The API database connection'
    Assert-ContainerConnection (Get-SettingValue $auth 'ConnectionStrings:Authentication') 'BookstoreAuth' $EnvironmentValues['MSSQL_SA_PASSWORD'] 'The Auth database connection'

    foreach ($pair in @(@($api, 'API'), @($auth, 'Auth'))) {
        $settings = $pair[0]
        $description = $pair[1]
        if ((Get-SettingValue $settings 'Kestrel:Certificates:Default:Path') -cne '/https/localhost.pfx') {
            throw "$description HTTPS certificate path is inconsistent. Restore matching ignored runtime files; this script will not replace them."
        }
        Assert-NonPlaceholderSecret (Get-SettingValue $settings 'Kestrel:Certificates:Default:Password') "$description HTTPS certificate password"
    }
    $httpsPassword = Get-SettingValue $api 'Kestrel:Certificates:Default:Password'
    if ($httpsPassword -cne (Get-SettingValue $auth 'Kestrel:Certificates:Default:Password')) {
        throw 'API and Auth HTTPS certificate passwords do not match. Restore matching ignored runtime files; this script will not rotate them.'
    }
    if ((Get-SettingValue $auth 'Auth:Issuer') -cne 'https://localhost:7200/' -or
        (Get-SettingValue $auth 'Auth:BrowserRedirectUris:0') -cne 'https://localhost:7200/demo/callback' -or
        (Get-SettingValue $auth 'Auth:BrowserRedirectUris:1') -cne 'https://localhost:7100/swagger/oauth2-redirect.html') {
        throw 'The existing OAuth issuer or callback configuration is inconsistent with the Docker demo. Restore matching ignored runtime files; this script will not overwrite them.'
    }
    Assert-NonPlaceholderSecret (Get-SettingValue $auth 'DevelopmentDemo:UserPassword') 'Demo-user password'
    Assert-NonPlaceholderSecret (Get-SettingValue $auth 'DevelopmentDemo:ManagementClientSecret') 'Management-client secret'

    foreach ($pair in @(@('Signing', $signingPfxPath), @('Encryption', $encryptionPfxPath))) {
        $purpose = $pair[0]
        $path = $pair[1]
        if ((Get-SettingValue $auth ("Certificates:$purpose`:Path")) -cne ("/oauth/" + $purpose.ToLowerInvariant() + '.pfx')) {
            throw "The existing OAuth $($purpose.ToLowerInvariant()) certificate path is inconsistent. Restore matching ignored runtime files; this script will not replace it."
        }
        $password = Get-SettingValue $auth ("Certificates:$purpose`:Password")
        Assert-NonPlaceholderSecret $password "OAuth $($purpose.ToLowerInvariant()) certificate password"
        $certificate = Get-ValidPfxCertificate $path $password "OAuth $($purpose.ToLowerInvariant()) certificate"
        $certificate.Dispose()
    }

    $httpsCertificate = Get-ValidPfxCertificate $httpsPfxPath $httpsPassword 'HTTPS certificate'
    try {
        $publicCertificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($httpsCertificatePath)
        try {
            if ($publicCertificate.Thumbprint -cne $httpsCertificate.Thumbprint) {
                throw 'The public HTTPS certificate does not match localhost.pfx. Restore matching ignored runtime files; this script will not replace them.'
            }
        }
        finally {
            $publicCertificate.Dispose()
        }
    }
    catch {
        if ($_.Exception.Message -like 'The public HTTPS certificate does not match*') { throw }
        throw 'The public HTTPS certificate is unreadable. Restore matching ignored runtime files; this script will not replace it.'
    }
    finally {
        $httpsCertificate.Dispose()
    }
}

function New-ContainerConnectionString {
    param([Parameter(Mandatory = $true)][string] $DatabaseName, [Parameter(Mandatory = $true)][string] $Password)

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder.DataSource = 'sqlserver,1433'
    $builder.InitialCatalog = $DatabaseName
    $builder.UserID = 'sa'
    $builder.Password = $Password
    $builder.Encrypt = $true
    $builder.TrustServerCertificate = $true
    return $builder.ConnectionString
}

function Export-NewCertificate {
    param(
        [Parameter(Mandatory = $true)][string] $Subject,
        [Parameter(Mandatory = $true)][string] $PfxPath,
        [Parameter(Mandatory = $true)][string] $Password,
        [string[]] $DnsName
    )

    $arguments = @{
        Subject = $Subject
        CertStoreLocation = 'Cert:\CurrentUser\My'
        KeyAlgorithm = 'RSA'
        KeyLength = 3072
        HashAlgorithm = 'SHA256'
        KeyExportPolicy = 'Exportable'
        NotAfter = (Get-Date).AddYears(2)
    }
    if ($null -ne $DnsName -and $DnsName.Count -gt 0) { $arguments['DnsName'] = $DnsName }
    $certificate = New-SelfSignedCertificate @arguments
    try {
        $securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
        Export-PfxCertificate -Cert $certificate -FilePath $PfxPath -Password $securePassword -ChainOption EndEntityCertOnly -NoProperties | Out-Null
        return $certificate.RawData
    }
    finally {
        Remove-Item -LiteralPath (Join-Path 'Cert:\CurrentUser\My' $certificate.Thumbprint) -Force
        $certificate.Dispose()
    }
}

function Write-PemCertificate {
    param([Parameter(Mandatory = $true)][byte[]] $RawData, [Parameter(Mandatory = $true)][string] $Path)

    $base64 = [Convert]::ToBase64String($RawData, [Base64FormattingOptions]::InsertLineBreaks)
    Write-Utf8File $Path ("-----BEGIN CERTIFICATE-----`n" + $base64 + "`n-----END CERTIFICATE-----`n")
}

function Trust-PublicHttpsCertificate {
    param([Parameter(Mandatory = $true)][string] $Path)

    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($Path)
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'CurrentUser')
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $matches = $store.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $certificate.Thumbprint, $false)
        if ($matches.Count -eq 0) { $store.Add($certificate) }
    }
    finally {
        $store.Close()
        $certificate.Dispose()
    }
}

$expectedRuntimeFiles = @($apiPath, $authPath, $httpsPfxPath, $httpsCertificatePath, $signingPfxPath, $encryptionPfxPath)
$presentRuntimeFiles = @($expectedRuntimeFiles | Where-Object { Test-Path -LiteralPath $_ })
if ($presentRuntimeFiles.Count -gt 0 -and $presentRuntimeFiles.Count -ne $expectedRuntimeFiles.Count) {
    throw 'The Docker runtime configuration is incomplete. Restore all matching ignored files before continuing; this script will not fill gaps or rotate credentials.'
}
if ($presentRuntimeFiles.Count -eq 0 -and (Test-Path -LiteralPath $dockerRoot)) {
    throw 'The existing .local/docker directory is not a complete Docker demo configuration. Preserve it and resolve it manually; this script will not overwrite it.'
}

$volumesExist = Get-DockerDemoVolumesExist
$environmentExisted = Test-Path -LiteralPath $environmentPath
if ($environmentExisted) {
    $environmentValues = Read-DockerEnvironment $environmentPath
}
else {
    $environmentValues = $null
}

if ($presentRuntimeFiles.Count -eq $expectedRuntimeFiles.Count) {
    if ($null -eq $environmentValues) {
        throw 'Existing Docker runtime files require their matching .env. Restore .env before continuing; this script will not generate a new SQL password.'
    }
    Assert-ExistingDockerRuntime $environmentValues
    if ($TrustHttpsCertificate) { Trust-PublicHttpsCertificate $httpsCertificatePath }
    Write-Output 'Existing Docker runtime files are valid and were preserved. No credentials, certificates, database data, or volumes changed.'
    Write-Output "Start the demo with: docker compose --profile demo up -d --build --wait"
    Write-Output "Copy the demo-user password deliberately: (Get-Content -LiteralPath '.local/docker/auth.json' -Raw | ConvertFrom-Json).'DevelopmentDemo:UserPassword' | Set-Clipboard"
    return
}

if ($volumesExist) {
    throw 'Existing Docker demo volumes were found without a complete matching runtime configuration. Restore .env and .local/docker from backup; this script will not generate replacement credentials.'
}

if ($null -eq $environmentValues) {
    $environmentValues = @{ MSSQL_SA_PASSWORD = New-RandomPassword; MSSQL_PORT = '14333' }
    $createEnvironment = $true
}
else {
    $createEnvironment = $false
}

[System.IO.Directory]::CreateDirectory($localRoot) | Out-Null
$stagingRoot = Join-Path $localRoot ('.docker-staging-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory((Join-Path $stagingRoot 'https')) | Out-Null
[System.IO.Directory]::CreateDirectory((Join-Path $stagingRoot 'oauth')) | Out-Null

try {
    $httpsPassword = New-RandomPassword
    $signingPassword = New-RandomPassword
    $encryptionPassword = New-RandomPassword
    $demoUserPassword = New-RandomPassword
    $managementClientSecret = New-RandomPassword
    $stagedHttpsPfx = Join-Path $stagingRoot 'https\localhost.pfx'
    $stagedHttpsCertificate = Join-Path $stagingRoot 'https\localhost.crt'
    $stagedSigningPfx = Join-Path $stagingRoot 'oauth\signing.pfx'
    $stagedEncryptionPfx = Join-Path $stagingRoot 'oauth\encryption.pfx'
    $httpsRawData = Export-NewCertificate 'CN=localhost' $stagedHttpsPfx $httpsPassword @('localhost')
    Write-PemCertificate $httpsRawData $stagedHttpsCertificate
    [void](Export-NewCertificate 'CN=Bookstore Docker demo signing' $stagedSigningPfx $signingPassword)
    [void](Export-NewCertificate 'CN=Bookstore Docker demo encryption' $stagedEncryptionPfx $encryptionPassword)
    foreach ($certificateInput in @(
            @($stagedHttpsPfx, $httpsPassword, 'Generated HTTPS certificate'),
            @($stagedSigningPfx, $signingPassword, 'Generated OAuth signing certificate'),
            @($stagedEncryptionPfx, $encryptionPassword, 'Generated OAuth encryption certificate'))) {
        $generatedCertificate = Get-ValidPfxCertificate $certificateInput[0] $certificateInput[1] $certificateInput[2]
        $generatedCertificate.Dispose()
    }

    $api = [ordered]@{
        'ConnectionStrings:Bookstore' = New-ContainerConnectionString 'Bookstore' $environmentValues['MSSQL_SA_PASSWORD']
        'Kestrel:Certificates:Default:Path' = '/https/localhost.pfx'
        'Kestrel:Certificates:Default:Password' = $httpsPassword
    }
    $auth = [ordered]@{
        'ConnectionStrings:Authentication' = New-ContainerConnectionString 'BookstoreAuth' $environmentValues['MSSQL_SA_PASSWORD']
        'Auth:Issuer' = 'https://localhost:7200/'
        'Auth:BrowserRedirectUris:0' = 'https://localhost:7200/demo/callback'
        'Auth:BrowserRedirectUris:1' = 'https://localhost:7100/swagger/oauth2-redirect.html'
        'DevelopmentDemo:ManagementClientSecret' = $managementClientSecret
        'DevelopmentDemo:UserPassword' = $demoUserPassword
        'Kestrel:Certificates:Default:Path' = '/https/localhost.pfx'
        'Kestrel:Certificates:Default:Password' = $httpsPassword
        'Certificates:Signing:Path' = '/oauth/signing.pfx'
        'Certificates:Signing:Password' = $signingPassword
        'Certificates:Encryption:Path' = '/oauth/encryption.pfx'
        'Certificates:Encryption:Password' = $encryptionPassword
    }
    Write-Utf8File (Join-Path $stagingRoot 'api.json') ($api | ConvertTo-Json -Depth 5)
    Write-Utf8File (Join-Path $stagingRoot 'auth.json') ($auth | ConvertTo-Json -Depth 5)

    if ($createEnvironment) {
        Write-Utf8File $environmentPath ("MSSQL_SA_PASSWORD=" + $environmentValues['MSSQL_SA_PASSWORD'] + "`nMSSQL_PORT=" + $environmentValues['MSSQL_PORT'] + "`n")
    }
    Move-Item -LiteralPath $stagingRoot -Destination $dockerRoot
}
catch {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
    throw
}

if ($TrustHttpsCertificate) { Trust-PublicHttpsCertificate $httpsCertificatePath }
Write-Output 'Docker runtime files were created in ignored .env and .local/docker. No credentials were printed.'
Write-Output 'Future runs validate and preserve these credentials and certificates; they do not rotate them.'
Write-Output "Start the demo with: docker compose --profile demo up -d --build --wait"
Write-Output "Copy the demo-user password deliberately: (Get-Content -LiteralPath '.local/docker/auth.json' -Raw | ConvertFrom-Json).'DevelopmentDemo:UserPassword' | Set-Clipboard"
