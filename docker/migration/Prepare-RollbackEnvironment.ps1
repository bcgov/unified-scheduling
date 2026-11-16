$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '.env.rollback.local'
if (Test-Path $path) { throw 'Refusing to overwrite the retained rollback key.' }
$created = $false
try {
    New-Item -ItemType File -Path $path | Out-Null
    $created = $true
    $acl = Get-Acl $path
    $acl.SetAccessRuleProtection($true, $false)
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl.SetOwner($owner)
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
        $owner, [Security.AccessControl.FileSystemRights]::FullControl,
        [Security.AccessControl.AccessControlType]::Allow)))
    Set-Acl -Path $path -AclObject $acl
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $lines = @(
        'DataMigrationRollback__EncryptionKey=' + [Convert]::ToBase64String($bytes)
        'DataMigrationRollback__KeyId=' + [Guid]::NewGuid().ToString()
    )
    [IO.File]::WriteAllLines($path, $lines, (New-Object Text.UTF8Encoding($false)))
    [Array]::Clear($bytes, 0, $bytes.Length)
    Write-Output $path
}
catch {
    if ($created -and (Test-Path $path)) { Remove-Item -LiteralPath $path }
    throw 'Restricted rollback key preparation failed.'
}
