using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Process;

[Mapper]
public partial class ProcessTemplateToProcessTemplateDtoMapper : TwoWayMapperBase<ProcessTemplate, ProcessTemplateDto>
{
    public override partial ProcessTemplateDto Map(ProcessTemplate source);
    public override partial void Map(ProcessTemplate source, ProcessTemplateDto destination);

    public override partial ProcessTemplate ReverseMap(ProcessTemplateDto destination);
    public override partial void ReverseMap(ProcessTemplateDto destination, ProcessTemplate source);
}

[Mapper]
public partial class CreateUpdateProcessTemplateDtoToProcessTemplateMapper : MapperBase<CreateUpdateProcessTemplateDto, ProcessTemplate>
{
    public override partial ProcessTemplate Map(CreateUpdateProcessTemplateDto source);
    public override partial void Map(CreateUpdateProcessTemplateDto source, ProcessTemplate destination);
}