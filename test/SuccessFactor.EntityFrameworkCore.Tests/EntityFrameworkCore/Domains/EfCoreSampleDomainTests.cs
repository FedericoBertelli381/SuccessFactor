using SuccessFactor.Samples;
using Xunit;

namespace SuccessFactor.EntityFrameworkCore.Domains;

[Collection(SuccessFactorTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<SuccessFactorEntityFrameworkCoreTestModule>
{

}
