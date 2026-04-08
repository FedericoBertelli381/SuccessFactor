using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace SuccessFactor.Employees;

[Mapper]
public partial class EmployeeToEmployeeDtoMapper : TwoWayMapperBase<Employee, EmployeeDto>
{
    public override partial EmployeeDto Map(Employee source);
    public override partial void Map(Employee source, EmployeeDto destination);

    public override partial Employee ReverseMap(EmployeeDto destination);
    public override partial void ReverseMap(EmployeeDto destination, Employee source);
}

[Mapper]
public partial class CreateUpdateEmployeeDtoToEmployeeMapper : MapperBase<CreateUpdateEmployeeDto, Employee>
{
    public override partial Employee Map(CreateUpdateEmployeeDto source);
    public override partial void Map(CreateUpdateEmployeeDto source, Employee destination);
}