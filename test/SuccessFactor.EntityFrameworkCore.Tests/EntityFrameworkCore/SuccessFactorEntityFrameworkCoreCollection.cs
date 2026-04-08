using Xunit;

namespace SuccessFactor.EntityFrameworkCore;

[CollectionDefinition(SuccessFactorTestConsts.CollectionDefinitionName)]
public class SuccessFactorEntityFrameworkCoreCollection : ICollectionFixture<SuccessFactorEntityFrameworkCoreFixture>
{

}
