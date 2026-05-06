param(
    [string]$ConnectionString = "Server=SQL15CF02\ISTDEV;Database=SuccessFactor_TEST;User Id=SuccessFactor_TEST_ow;Password=lWNhdvlDJw,Q4O4HKlwQ;Encrypt=True;TrustServerCertificate=True",
    [string]$RepoRoot = "C:\Progetti\SuccessFactor",
    [string]$TenantName = "LeSoluzioni Scarl",
    [string]$AdminUserName = "admin",
	[string]$AdminEmail = "c.esposito@lesoluzioni.net"
)

$ErrorActionPreference = "Stop"

# Hash ASP.NET Identity valido per password "q"
$AdminPasswordHash = "AQAAAAIAAYagAAAAEANxg2bxecMDq1o4xvHLV0TfiQIpVP+nSfQscxyyUgDA63XI70smVKWecCENl7Jp9w=="

function New-SqlConnection {
    param([string]$Cs)
    $cn = New-Object System.Data.SqlClient.SqlConnection($Cs)
    $cn.Open()
    return $cn
}

function Invoke-SqlScalar {
    param(
        [string]$Cs,
        [string]$Sql,
        [hashtable]$Parameters = @{}
    )

    $cn = New-SqlConnection -Cs $Cs
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = $Sql
        foreach ($key in $Parameters.Keys) {
            [void]$cmd.Parameters.AddWithValue("@$key", $Parameters[$key])
        }
        return $cmd.ExecuteScalar()
    }
    finally {
        $cn.Close()
    }
}

function Invoke-SqlNonQuery {
    param(
        [string]$Cs,
        [string]$Sql,
        [hashtable]$Parameters = @{}
    )

    $cn = New-SqlConnection -Cs $Cs
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = $Sql
        foreach ($key in $Parameters.Keys) {
            [void]$cmd.Parameters.AddWithValue("@$key", $Parameters[$key])
        }
        return $cmd.ExecuteNonQuery()
    }
    finally {
        $cn.Close()
    }
}

$normalizedTenantName = $TenantName.ToUpperInvariant()
$normalizedUserName = $AdminUserName.ToUpperInvariant()
$normalizedEmail = $AdminEmail.ToUpperInvariant()

# 1. TENANT
$tenantId = Invoke-SqlScalar -Cs $ConnectionString -Sql @"
SELECT TOP 1 Id
FROM dbo.AbpTenants
WHERE Name = @Name
"@ -Parameters @{
    Name = $TenantName
}

if (-not $tenantId) {
    $tenantId = [Guid]::NewGuid()

    Invoke-SqlNonQuery -Cs $ConnectionString -Sql @"
INSERT INTO dbo.AbpTenants
(
    Id,
    Name,
    NormalizedName,
    ExtraProperties,
    ConcurrencyStamp,
    CreationTime,
    IsDeleted,
    EntityVersion
)
VALUES
(
    @Id,
    @Name,
    @NormalizedName,
    N'{}',
    @ConcurrencyStamp,
    SYSUTCDATETIME(),
    0,
    0
)
"@ -Parameters @{
        Id = $tenantId
        Name = $TenantName
        NormalizedName = $normalizedTenantName
        ConcurrencyStamp = ([Guid]::NewGuid().ToString("N"))
    }

    Write-Host "Tenant creato: $TenantName ($tenantId)"
}
else {
    $tenantId = [Guid]$tenantId
    Write-Host "Tenant gia' esistente: $TenantName ($tenantId)"
}

# 2. RUOLO ADMIN DEL TENANT
$roleId = Invoke-SqlScalar -Cs $ConnectionString -Sql @"
SELECT TOP 1 Id
FROM dbo.AbpRoles
WHERE TenantId = @TenantId
  AND Name = N'admin'
"@ -Parameters @{
    TenantId = $tenantId
}

if (-not $roleId) {
    $roleId = [Guid]::NewGuid()

    Invoke-SqlNonQuery -Cs $ConnectionString -Sql @"
INSERT INTO dbo.AbpRoles
(
    Id,
    TenantId,
    Name,
    NormalizedName,
    IsDefault,
    IsStatic,
    IsPublic,
    ExtraProperties,
    ConcurrencyStamp,
    CreationTime,
    EntityVersion
)
VALUES
(
    @Id,
    @TenantId,
    N'admin',
    N'ADMIN',
    0,
    0,
    1,
    N'{}',
    @ConcurrencyStamp,
    SYSUTCDATETIME(),
    0
)
"@ -Parameters @{
        Id = $roleId
        TenantId = $tenantId
        ConcurrencyStamp = ([Guid]::NewGuid().ToString("N"))
    }

    Write-Host "Ruolo admin creato nel tenant."
}
else {
    $roleId = [Guid]$roleId
    Write-Host "Ruolo admin gia' esistente nel tenant."
}

# 3. UTENTE ADMIN DEL TENANT
$userId = Invoke-SqlScalar -Cs $ConnectionString -Sql @"
SELECT TOP 1 Id
FROM dbo.AbpUsers
WHERE TenantId = @TenantId
  AND UserName = @UserName
  AND IsDeleted = 0
"@ -Parameters @{
    TenantId = $tenantId
    UserName = $AdminUserName
}

if (-not $userId) {
    $userId = [Guid]::NewGuid()

    Invoke-SqlNonQuery -Cs $ConnectionString -Sql @"
INSERT INTO dbo.AbpUsers
(
    Id,
    TenantId,
    UserName,
    NormalizedUserName,
    Name,
    Surname,
    Email,
    NormalizedEmail,
    EmailConfirmed,
    PasswordHash,
    SecurityStamp,
    ConcurrencyStamp,
    PhoneNumberConfirmed,
    TwoFactorEnabled,
    LockoutEnabled,
    AccessFailedCount,
    IsActive,
    IsExternal,
    ShouldChangePasswordOnNextLogin,
    ExtraProperties,
    CreationTime,
    IsDeleted,
    EntityVersion
)
VALUES
(
    @Id,
    @TenantId,
    @UserName,
    @NormalizedUserName,
    @Name,
    @Surname,
    @Email,
    @NormalizedEmail,
    1,
    @PasswordHash,
    @SecurityStamp,
    @ConcurrencyStamp,
    0,
    0,
    0,
    0,
    1,
    0,
    0,
    N'{}',
    SYSUTCDATETIME(),
    0,
    0
)
"@ -Parameters @{
        Id = $userId
        TenantId = $tenantId
        UserName = $AdminUserName
        NormalizedUserName = $normalizedUserName
        Name = "Admin"
        Surname = "LeSoluzioni"
        Email = $AdminEmail
        NormalizedEmail = $normalizedEmail
        PasswordHash = $AdminPasswordHash
        SecurityStamp = ([Guid]::NewGuid().ToString("N"))
        ConcurrencyStamp = ([Guid]::NewGuid().ToString("N"))
    }

    Write-Host "Utente admin creato."
}
else {
    $userId = [Guid]$userId

    Invoke-SqlNonQuery -Cs $ConnectionString -Sql @"
UPDATE dbo.AbpUsers
SET
    Email = @Email,
    NormalizedEmail = @NormalizedEmail,
    PasswordHash = @PasswordHash,
    SecurityStamp = @SecurityStamp,
    ConcurrencyStamp = @ConcurrencyStamp,
    AccessFailedCount = 0,
    LockoutEnd = NULL,
    IsActive = 1,
    EmailConfirmed = 1,
    ShouldChangePasswordOnNextLogin = 0
WHERE Id = @UserId
"@ -Parameters @{
        UserId = $userId
        Email = $AdminEmail
        NormalizedEmail = $normalizedEmail
        PasswordHash = $AdminPasswordHash
        SecurityStamp = ([Guid]::NewGuid().ToString("N"))
        ConcurrencyStamp = ([Guid]::NewGuid().ToString("N"))
    }

    Write-Host "Utente admin gia' esistente: password aggiornata a q."
}

# 4. ASSEGNAZIONE RUOLO ADMIN
$userRoleExists = Invoke-SqlScalar -Cs $ConnectionString -Sql @"
SELECT COUNT(1)
FROM dbo.AbpUserRoles
WHERE UserId = @UserId
  AND RoleId = @RoleId
"@ -Parameters @{
    UserId = $userId
    RoleId = $roleId
}

if ([int]$userRoleExists -eq 0) {
    Invoke-SqlNonQuery -Cs $ConnectionString -Sql @"
INSERT INTO dbo.AbpUserRoles
(
    UserId,
    RoleId,
    TenantId
)
VALUES
(
    @UserId,
    @RoleId,
    @TenantId
)
"@ -Parameters @{
        UserId = $userId
        RoleId = $roleId
        TenantId = $tenantId
    }

    Write-Host "Ruolo admin assegnato all'utente."
}
else {
    Write-Host "Utente gia' collegato al ruolo admin."
}

Write-Host ""
Write-Host "Completato."
Write-Host "TenantId: $tenantId"
Write-Host "Tenant: $TenantName"
Write-Host "Admin username: $AdminUserName"
Write-Host "Admin email: $AdminEmail"
Write-Host "Admin password: q"
Write-Host "URL: http://127.0.0.1:8080/?__tenant=LeSoluzioni%20Scarl"


