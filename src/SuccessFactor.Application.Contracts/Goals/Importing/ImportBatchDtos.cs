using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace SuccessFactor.Goals.Importing;

public class ImportBatchDto : EntityDto<Guid>
{
    public Guid CycleId { get; set; }
    public string FileName { get; set; } = default!;
    public string Status { get; set; } = default!;
}

public class CreateImportBatchDto
{
    [Required] public Guid CycleId { get; set; }
    [Required, StringLength(260)] public string FileName { get; set; } = default!;
}