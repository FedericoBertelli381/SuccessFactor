using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Employees;

[Mapper]
public partial class AssignManagerDtoToEmployeeManagerMapper : MapperBase<AssignManagerDto, EmployeeManager>
{
    public override partial EmployeeManager Map(AssignManagerDto source);
    public override partial void Map(AssignManagerDto source, EmployeeManager destination);
}