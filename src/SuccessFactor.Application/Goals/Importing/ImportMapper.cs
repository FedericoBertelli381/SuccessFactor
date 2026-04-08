using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Goals.Importing;

[Mapper]
public partial class GoalImportBatchToImportBatchDtoMapper : TwoWayMapperBase<GoalImportBatch, ImportBatchDto>
{
    public override partial ImportBatchDto Map(GoalImportBatch source);
    public override partial void Map(GoalImportBatch source, ImportBatchDto destination);

    public override partial GoalImportBatch ReverseMap(ImportBatchDto destination);
    public override partial void ReverseMap(ImportBatchDto destination, GoalImportBatch source);
}

[Mapper]
public partial class GoalProgressBatchToImportBatchDtoMapper : TwoWayMapperBase<GoalProgressBatch, ImportBatchDto>
{
    public override partial ImportBatchDto Map(GoalProgressBatch source);
    public override partial void Map(GoalProgressBatch source, ImportBatchDto destination);

    public override partial GoalProgressBatch ReverseMap(ImportBatchDto destination);
    public override partial void ReverseMap(ImportBatchDto destination, GoalProgressBatch source);
}