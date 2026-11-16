$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '.env.oracle.local'
$target = Join-Path $PSScriptRoot '.env.oracle.ready-v4.local'
if ((Test-Path $target) -or ((Get-Item $source).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Refusing existing target or linked source.'
}
$lines = [IO.File]::ReadAllLines($source)
$matches = @($lines | Where-Object { $_.StartsWith('SS_SYNTHETIC_DB=', [StringComparison]::Ordinal) })
if ($matches.Count -ne 1) { throw 'Expected exactly one source connection setting.' }
try {
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection.ConnectionString = $matches[0].Substring('SS_SYNTHETIC_DB='.Length)
    $connection['Database'] = 'ss_ready_v4'
    $replacement = 'SS_SYNTHETIC_DB=' + $connection.ConnectionString
    $output = @($lines | ForEach-Object {
        if ($_.StartsWith('SS_SYNTHETIC_DB=', [StringComparison]::Ordinal)) { $replacement } else { $_ }
    })
    # Create EMPTY, restrict ACL, then write credentials; never leave an unrestricted
    # populated file. Windows PowerShell and PowerShell 7 compatible.
    New-Item -ItemType File -Path $target | Out-Null
    $acl = Get-Acl $target
    $acl.SetAccessRuleProtection($true, $false)
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl.SetOwner($owner)
    $rule = New-Object Security.AccessControl.FileSystemAccessRule(
        $owner, [Security.AccessControl.FileSystemRights]::FullControl,
        [Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($rule)
    Set-Acl -Path $target -AclObject $acl
    [IO.File]::WriteAllLines($target, $output, (New-Object Text.UTF8Encoding($false)))
    Write-Output $target
}
catch {
    if (Test-Path $target) { Remove-Item -LiteralPath $target }
    # Connection parser errors can contain credentials. Do not propagate them.
    throw 'Restricted ready-v4 oracle environment preparation failed.'
}
