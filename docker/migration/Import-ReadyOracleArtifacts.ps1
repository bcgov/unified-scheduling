$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'artifacts'
$source = 'C:\Dev\CSB\Unified\legacy\sheriff-scheduling\test-harness\report-oracle\artifacts'
$files = @{
    'probe-v4-availability-v1-e6e674c.json' = '874D6B412686D940517685C0A3D4BF73F5A3D866AE11B19E55CF70D0051EB9F6'
    'ready-v4-source-all-v1-80e9aae.json' = 'DC28370841D1B444D3950E897EDEE2AA268ACC1EDAD7DFDCCDEECA2B1EDC62C7'
    'ready-v4-source-training-v1-80e9aae.json' = '4B4FA1DA4A49319E5287006192CF3838443174CF23B338DA0376C47209AEDD9B'
    'ready-v4-availability-v1-e6e674c.json' = '002C5DB06A157EB1A79413608E66C0E437FFC362C1DB194B4D8E90DF1BB88D24'
    'ready-v4-source-summary-v1-e6e674c.json' = '50FFF6DF4E761D4C7F3674059B6AC628732B2867C5E6857A4E779E5B73EF4D41'
}
foreach ($name in $files.Keys) {
    $from = Join-Path $source $name
    $to = Join-Path $root $name
    if ((Get-Item $from).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked input refused.' }
    $hash = (Get-FileHash $from -Algorithm SHA256).Hash
    if ($files[$name] -and $hash -cne $files[$name]) { throw 'Independent capture digest mismatch.' }
    if (Test-Path $to) {
        if ((Get-Item $to).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked destination refused.' }
        if ((Get-FileHash $to -Algorithm SHA256).Hash -cne $hash) { throw 'Existing artifact differs; never overwrite.' }
    } else {
        New-Item -ItemType File -Path $to | Out-Null
        $acl = Get-Acl $to
        $acl.SetAccessRuleProtection($true, $false)
        $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
        $acl.SetOwner($owner)
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
            $owner, [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.AccessControlType]::Allow)))
        Set-Acl -Path $to -AclObject $acl
        [IO.File]::WriteAllBytes($to, [IO.File]::ReadAllBytes($from))
    }
    Write-Output "$name SHA256=$hash"
}
