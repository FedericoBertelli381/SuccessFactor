using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using SuccessFactor.Security;
using Volo.Abp.Security.Claims;

namespace SuccessFactor.Blazor.Security;

public class CorporateSsoClaimsTransformation : IClaimsTransformation
{
    private static readonly string[] ExternalRoleClaimTypes =
    [
        ClaimTypes.Role,
        AbpClaimTypes.Role,
        "role",
        "roles",
        "groups"
    ];

    private readonly IConfiguration _configuration;

    public CorporateSsoClaimsTransformation(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var mappedRoles = ResolveMappedRoles(principal);

        foreach (var role in mappedRoles)
        {
            AddRoleClaimIfMissing(identity, ClaimTypes.Role, role);
            AddRoleClaimIfMissing(identity, AbpClaimTypes.Role, role);
        }

        return Task.FromResult(principal);
    }

    private HashSet<string> ResolveMappedRoles(ClaimsPrincipal principal)
    {
        var mappedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var externalValues = principal.Claims
            .Where(x => ExternalRoleClaimTypes.Contains(x.Type, StringComparer.OrdinalIgnoreCase))
            .SelectMany(x => x.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var value in externalValues)
        {
            var normalized = SuccessFactorRoles.Normalize(value);
            if (normalized is not null)
            {
                mappedRoles.Add(normalized);
            }
        }

        var mappings = _configuration.GetSection("Sso:RoleMappings").GetChildren();

        foreach (var mapping in mappings)
        {
            var appRole = SuccessFactorRoles.Normalize(mapping.Key);
            if (appRole is null)
            {
                continue;
            }

            var acceptedExternalValues = mapping.GetChildren()
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x));

            if (acceptedExternalValues.Any(x => externalValues.Contains(x!)))
            {
                mappedRoles.Add(appRole);
            }
        }

        return mappedRoles;
    }

    private static void AddRoleClaimIfMissing(ClaimsIdentity identity, string claimType, string role)
    {
        if (!identity.HasClaim(x => string.Equals(x.Type, claimType, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(x.Value, role, StringComparison.OrdinalIgnoreCase)))
        {
            identity.AddClaim(new Claim(claimType, role));
        }
    }
}
