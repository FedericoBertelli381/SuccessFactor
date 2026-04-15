using System.Threading.Tasks;
using SuccessFactor.Localization;
using SuccessFactor.Permissions;
using SuccessFactor.MultiTenancy;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.UI.Navigation;
using Volo.Abp.SettingManagement.Blazor.Menus;
using Volo.Abp.TenantManagement.Blazor.Navigation;
using Volo.Abp.Identity.Blazor;

namespace SuccessFactor.Blazor.Menus;

public class SuccessFactorMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<SuccessFactorResource>();
        
        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                SuccessFactorMenus.Home,
                l["Menu:Home"],
                "/",
                icon: "fas fa-home",
                order: 1
            )
        );

        context.Menu.Items.Insert(
            1,
            new ApplicationMenuItem(
                SuccessFactorMenus.My,
                "My",
                "/my",
                icon: "fas fa-user",
                order: 2
            )
        );

        context.Menu.Items.Insert(
            2,
            new ApplicationMenuItem(
                SuccessFactorMenus.Team,
                "Team",
                "/team",
                icon: "fas fa-users",
                order: 3
            )
        );

        context.Menu.Items.Insert(
            3,
            new ApplicationMenuItem(
                SuccessFactorMenus.Hr,
                "HR",
                "/hr",
                icon: "fas fa-building",
                order: 4
            )
        );

        context.Menu.Items.Insert(
            4,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminCycles,
                "Admin Cycles",
                "/admin/cycles",
                icon: "fas fa-calendar-alt",
                order: 5
            )
        );

        context.Menu.Items.Insert(
            5,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminCycleParticipants,
                "Admin Participants",
                "/admin/cycle-participants",
                icon: "fas fa-user-check",
                order: 6
            )
        );

        context.Menu.Items.Insert(
            6,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminManagerRelations,
                "Admin Managers",
                "/admin/manager-relations",
                icon: "fas fa-user-tie",
                order: 7
            )
        );

        context.Menu.Items.Insert(
            7,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminPerformanceImport,
                "Admin Import",
                "/admin/performance-import",
                icon: "fas fa-file-import",
                order: 8
            )
        );

        context.Menu.Items.Insert(
            8,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminReadiness,
                "Admin Readiness",
                "/admin/readiness",
                icon: "fas fa-clipboard-check",
                order: 9
            )
        );

        context.Menu.Items.Insert(
            9,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminWorkflow,
                "Admin Workflow",
                "/admin/workflow",
                icon: "fas fa-sliders-h",
                order: 10
            )
        );

        context.Menu.Items.Insert(
            10,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminEmployees,
                "Admin Employees",
                "/admin/employees",
                icon: "fas fa-id-badge",
                order: 11
            )
        );

        context.Menu.Items.Insert(
            11,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminOrgUnits,
                "Admin OrgUnits",
                "/admin/org-units",
                icon: "fas fa-sitemap",
                order: 12
            )
        );

        context.Menu.Items.Insert(
            12,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminOrgChart,
                "Admin Org Chart",
                "/admin/org-chart",
                icon: "fas fa-project-diagram",
                order: 13
            )
        );

        context.Menu.Items.Insert(
            13,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminJobRoles,
                "Admin JobRoles",
                "/admin/job-roles",
                icon: "fas fa-briefcase",
                order: 14
            )
        );

        context.Menu.Items.Insert(
            14,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminGoalCatalog,
                "Admin Goal Catalog",
                "/admin/goal-catalog",
                icon: "fas fa-bullseye",
                order: 15
            )
        );

        context.Menu.Items.Insert(
            15,
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminIdentityLink,
                "Admin Users",
                "/admin/identity-link",
                icon: "fas fa-user-link",
                order: 16
            )
        );

        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 17;
    
        if (MultiTenancyConsts.IsEnabled)
        {
            administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 1);
        }
        else
        {
            administration.TryRemoveMenuItem(TenantManagementMenuNames.GroupName);
        }

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 3);

        return Task.CompletedTask;
    }
}
