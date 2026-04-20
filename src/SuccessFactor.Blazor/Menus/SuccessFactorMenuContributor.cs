using System.Threading.Tasks;
using SuccessFactor.Localization;
using SuccessFactor.MultiTenancy;
using SuccessFactor.Security;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.UI.Navigation;
using Volo.Abp.SettingManagement.Blazor.Menus;
using Volo.Abp.TenantManagement.Blazor.Navigation;
using Volo.Abp.Identity.Blazor;
using Volo.Abp.Users;

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
        var currentUser = context.ServiceProvider.GetRequiredService<ICurrentUser>();
        var isAdmin = SuccessFactorRoles.IsAdmin(currentUser.Roles);
        var isHr = SuccessFactorRoles.IsHr(currentUser.Roles);
        var isManager = SuccessFactorRoles.IsManager(currentUser.Roles);
        
        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.Home,
                l["Menu:Home"],
                "/",
                icon: "fas fa-home",
                order: 1
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.My,
                "My",
                "/my",
                icon: "fas fa-user",
                order: 2
            )
        );

        if (isAdmin || isManager)
        {
        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.Team,
                "Team",
                "/team",
                icon: "fas fa-users",
                order: 3
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.TeamReports,
                "Team Reports",
                "/team/reports",
                icon: "fas fa-chart-line",
                order: 4
            )
        );

        }

        if (isAdmin || isHr)
        {
        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.Hr,
                "HR",
                "/hr",
                icon: "fas fa-building",
                order: 5
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.HrReports,
                "HR Reports",
                "/hr/reports",
                icon: "fas fa-chart-bar",
                order: 6
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.PerformanceDashboard,
                "Performance Dashboard",
                "/hr/performance-dashboard",
                icon: "fas fa-chart-pie",
                order: 7
            )
        );

        }

        if (isAdmin)
        {
        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminCycles,
                "Admin Cycles",
                "/admin/cycles",
                icon: "fas fa-calendar-alt",
                order: 8
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminCycleParticipants,
                "Admin Participants",
                "/admin/cycle-participants",
                icon: "fas fa-user-check",
                order: 9
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminManagerRelations,
                "Admin Managers",
                "/admin/manager-relations",
                icon: "fas fa-user-tie",
                order: 10
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminPerformanceImport,
                "Admin Import",
                "/admin/performance-import",
                icon: "fas fa-file-import",
                order: 11
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminReadiness,
                "Admin Readiness",
                "/admin/readiness",
                icon: "fas fa-clipboard-check",
                order: 12
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminAuditLog,
                "Admin Audit Log",
                "/admin/audit-log",
                icon: "fas fa-history",
                order: 13
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminNotifications,
                "Admin Notifications",
                "/admin/notifications",
                icon: "fas fa-envelope",
                order: 14
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminWorkflow,
                "Admin Workflow",
                "/admin/workflow",
                icon: "fas fa-sliders-h",
                order: 15
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminEmployees,
                "Admin Employees",
                "/admin/employees",
                icon: "fas fa-id-badge",
                order: 16
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminOrgUnits,
                "Admin OrgUnits",
                "/admin/org-units",
                icon: "fas fa-sitemap",
                order: 17
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminOrgChart,
                "Admin Org Chart",
                "/admin/org-chart",
                icon: "fas fa-project-diagram",
                order: 18
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminJobRoles,
                "Admin JobRoles",
                "/admin/job-roles",
                icon: "fas fa-briefcase",
                order: 19
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminGoalCatalog,
                "Admin Goal Catalog",
                "/admin/goal-catalog",
                icon: "fas fa-bullseye",
                order: 20
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminGoalAssignments,
                "Admin Goal Assignments",
                "/admin/goal-assignments",
                icon: "fas fa-tasks",
                order: 21
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminCompetencyCatalog,
                "Admin Competencies",
                "/admin/competency-catalog",
                icon: "fas fa-brain",
                order: 22
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminCompetencyModels,
                "Admin Competency Models",
                "/admin/competency-models",
                icon: "fas fa-layer-group",
                order: 23
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminAssessmentSetup,
                "Admin Assessment Setup",
                "/admin/assessment-setup",
                icon: "fas fa-clipboard-list",
                order: 24
            )
        );

        context.Menu.Items.Add(
            new ApplicationMenuItem(
                SuccessFactorMenus.AdminIdentityLink,
                "Admin Users",
                "/admin/identity-link",
                icon: "fas fa-user-link",
                order: 25
            )
        );

        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 26;
    
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
        }

        return Task.CompletedTask;
    }
}
