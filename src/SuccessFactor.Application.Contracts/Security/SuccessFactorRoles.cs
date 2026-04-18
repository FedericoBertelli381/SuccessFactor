using System;
using System.Collections.Generic;
using System.Linq;

namespace SuccessFactor.Security;

public static class SuccessFactorRoles
{
    public const string Admin = "admin";
    public const string Hr = "HR";
    public const string Manager = "Responsabile";
    public const string Employee = "Dipendente";

    public const string AdminOrHr = Admin + "," + Hr;
    public const string AdminOrManager = Admin + "," + Manager;
    public const string AdminOrHrOrManager = Admin + "," + Hr + "," + Manager;

    private static readonly Dictionary<string, string> CanonicalRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        [Admin] = Admin,
        ["administrator"] = Admin,
        ["amministratore"] = Admin,
        [Hr] = Hr,
        ["human resources"] = Hr,
        ["risorse umane"] = Hr,
        [Manager] = Manager,
        ["manager"] = Manager,
        ["responsabile"] = Manager,
        [Employee] = Employee,
        ["employee"] = Employee,
        ["dipendente"] = Employee
    };

    public static string? Normalize(string? role)
        => string.IsNullOrWhiteSpace(role)
            ? null
            : CanonicalRoles.GetValueOrDefault(role.Trim());

    public static bool IsAdmin(IEnumerable<string>? roles)
        => HasRole(roles, Admin);

    public static bool IsHr(IEnumerable<string>? roles)
        => HasRole(roles, Hr);

    public static bool IsManager(IEnumerable<string>? roles)
        => HasRole(roles, Manager);

    public static bool IsAdminOrHr(IEnumerable<string>? roles)
        => IsAdmin(roles) || IsHr(roles);

    public static bool HasRole(IEnumerable<string>? roles, string role)
    {
        var expected = Normalize(role);

        return expected is not null &&
               roles is not null &&
               roles.Any(x => string.Equals(Normalize(x), expected, StringComparison.OrdinalIgnoreCase));
    }
}
