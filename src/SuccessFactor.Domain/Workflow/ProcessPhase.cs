using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace SuccessFactor.Workflow;

public class ProcessPhase : Entity<Guid>,
    ICreationAuditedObject, IModificationAuditedObject
{
    public Guid TemplateId { get; set; }            // FK -> ProcessTemplates (tenant via template)

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int PhaseOrder { get; set; }
    public bool IsTerminal { get; set; }

    public string? StartRule { get; set; }
    public string? EndRule { get; set; }

    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public byte[] RowVer { get; set; } = default!;
}