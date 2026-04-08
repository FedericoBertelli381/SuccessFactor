using SuccessFactor.Samples;
using Xunit;

namespace SuccessFactor.EntityFrameworkCore.Applications;

[Collection(SuccessFactorTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<SuccessFactorEntityFrameworkCoreTestModule>
{

}
