param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.artifacts/postgres')) + [IO.Path]::DirectorySeparatorChar
$target = [IO.Path]::GetFullPath($Directory)
if (-not $target.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $target)) {
    throw 'Certificate material requires a new directory inside the isolated PostgreSQL artifacts.'
}
[void][IO.Directory]::CreateDirectory($target)
if ($IsWindows) {
    # A chave efêmera fica acessível somente ao usuário executor e SYSTEM, sem alterar trust stores.
    $acl = Get-Acl -LiteralPath $target
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        [void]$acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $target -AclObject $acl
} else {
    [IO.File]::SetUnixFileMode($target, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
}
$rootKey = [Security.Cryptography.RSA]::Create(2048)
$serverKey = [Security.Cryptography.RSA]::Create(2048)
$untrustedKey = [Security.Cryptography.RSA]::Create(2048)
$root = $null
$server = $null
$untrusted = $null
try {
    $now = [DateTimeOffset]::UtcNow
    $rootRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=ORBIS isolated test root', $rootKey, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    [void]$rootRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
    [void]$rootRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign, $true))
    $root = $rootRequest.CreateSelfSigned($now.AddMinutes(-5), $now.AddDays(2))
    $serverRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=orbis-postgres.test', $serverKey, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    [void]$serverRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
    $usage = [Security.Cryptography.OidCollection]::new()
    [void]$usage.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))
    [void]$serverRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usage, $true))
    $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    $san.AddIpAddress([Net.IPAddress]::Parse('127.0.0.1'))
    $san.AddDnsName('*.orbis.test')
    [void]$serverRequest.CertificateExtensions.Add($san.Build())
    $serial = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $serial[0] = 1
    $server = $serverRequest.Create($root, $now.AddMinutes(-5), $now.AddDays(1), $serial)
    $untrustedRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=ORBIS untrusted negative-test root', $untrustedKey, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    [void]$untrustedRequest.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
    $untrusted = $untrustedRequest.CreateSelfSigned($now.AddMinutes(-5), $now.AddDays(2))
    [IO.File]::WriteAllText((Join-Path $target 'root.crt'), $root.ExportCertificatePem())
    [IO.File]::WriteAllText((Join-Path $target 'server.crt'), $server.ExportCertificatePem())
    [IO.File]::WriteAllText((Join-Path $target 'server.key'), $serverKey.ExportPkcs8PrivateKeyPem())
    [IO.File]::WriteAllText((Join-Path $target 'untrusted-root.crt'), $untrusted.ExportCertificatePem())
    if (-not $IsWindows) { [IO.File]::SetUnixFileMode((Join-Path $target 'server.key'), [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite) }
    # Nenhuma chave de CA é persistida; os clientes recebem somente a raiz pública por configuração explícita.
    [PSCustomObject]@{ Root = Join-Path $target 'root.crt'; Certificate = Join-Path $target 'server.crt'; Key = Join-Path $target 'server.key' }
} finally {
    if ($root) { $root.Dispose() }
    if ($server) { $server.Dispose() }
    if ($untrusted) { $untrusted.Dispose() }
    $rootKey.Dispose(); $serverKey.Dispose(); $untrustedKey.Dispose()
}
