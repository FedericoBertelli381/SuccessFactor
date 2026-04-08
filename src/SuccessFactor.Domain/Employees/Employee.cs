using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SuccessFactor.Employees;

public class Employee : Entity<Guid>, IMultiTenant,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }          // collegamento futuro ad Identity (AspNetUsers.Id)
    public string Matricola { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }

    public Guid? OrgUnitId { get; set; }
    public Guid? JobRoleId { get; set; }

    public bool IsActive { get; set; }

    // Auditing (mappati su CreatedAt/CreatedByUserId/ModifiedAt/ModifiedByUserId)
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}