using Microsoft.AspNetCore.Authorization;
using SuccessFactor.Cycles;
using SuccessFactor.Employees;
using SuccessFactor.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace SuccessFactor.Hr;

[Authorize]
public class HrDashboardAppService : ApplicationService, IHrDashboardAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IRepository<Cycle, Guid> _cycleRepository;
    private readonly IRepository<CycleParticipant, Guid> _cycleParticipantRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<ProcessPhase, Guid> _processPhaseRepository;

    public HrDashboardAppService(
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IRepository<Cycle, Guid> cycleRepository,
        IRepository<CycleParticipant, Guid> cycleParticipantRepository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<ProcessPhase, Guid> processPhaseRepository)
    {
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _cycleRepository = cycleRepository;
        _cycleParticipantRepository = cycleParticipantRepository;
        _employeeRepository = employeeRepository;
        _processPhaseRepository = processPhaseRepository;
    }

    public async Task<HrDashboardDto> GetAsync(GetHrDashboardInput input)
    {
        EnsureCurrentUserIsHr();

        input ??= new GetHrDashboardInput();

        var cycleQuery = await _cycleRepository.GetQueryableAsync();
        var cycles = await _asyncExecuter.ToListAsync(
            cycleQuery
                .OrderByDescending(x => x.CycleYear)
                .ThenByDescending(x => x.CreationTime)
                .ThenBy(x => x.Name));

        var selectedCycle = ResolveSelectedCycle(cycles, input.CycleId);

        var dto = new HrDashboardDto
        {
            SelectedCycleId = selectedCycle?.Id,
            SelectedCycleName = selectedCycle?.Name,
            SelectedCycleStatus = selectedCycle?.Status,
            Cycles = cycles
                .Select(x => new HrCycleLookupDto
                {
                    CycleId = x.Id,
                    CycleName = x.Name,
                    CycleStatus = x.Status,
                    IsSelected = selectedCycle is not null && x.Id == selectedCycle.Id
                })
                .ToList()
        };

        if (selectedCycle is null)
        {
            return dto;
        }

        var participantQuery = await _cycleParticipantRepository.GetQueryableAsync();
        var participants = await _asyncExecuter.ToListAsync(
            participantQuery
                .Where(x => x.CycleId == selectedCycle.Id)
                .OrderBy(x => x.EmployeeId));

        var employeeIds = participants
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToList();

        var phaseIds = participants
            .Where(x => x.CurrentPhaseId.HasValue)
            .Select(x => x.CurrentPhaseId!.Value)
            .Distinct()
            .ToList();

        var employees = new List<Employee>();

        if (employeeIds.Count > 0)
        {
            var employeeQuery = await _employeeRepository.GetQueryableAsync();
            employees = await _asyncExecuter.ToListAsync(
                employeeQuery.Where(x => employeeIds.Contains(x.Id)));
        }

        var phases = new List<ProcessPhase>();

        if (phaseIds.Count > 0)
        {
            var phaseQuery = await _processPhaseRepository.GetQueryableAsync();
            phases = await _asyncExecuter.ToListAsync(
                phaseQuery.Where(x => phaseIds.Contains(x.Id)));
        }

        var employeeById = employees.ToDictionary(x => x.Id, x => x);
        var phaseById = phases.ToDictionary(x => x.Id, x => x);

        dto.Participants = participants
            .Select(x =>
            {
                employeeById.TryGetValue(x.EmployeeId, out var employee);

                ProcessPhase? phase = null;
                if (x.CurrentPhaseId.HasValue)
                {
                    phaseById.TryGetValue(x.CurrentPhaseId.Value, out phase);
                }

                return new HrCycleParticipantDto
                {
                    EmployeeId = x.EmployeeId,
                    Matricola = employee?.Matricola ?? string.Empty,
                    FullName = employee?.FullName ?? string.Empty,
                    Email = employee?.Email,
                    ParticipantStatus = x.Status,
                    CurrentPhaseId = x.CurrentPhaseId,
                    CurrentPhaseCode = phase?.Code,
                    CurrentPhaseName = phase?.Name
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();

        dto.TotalParticipants = dto.Participants.Count;
        dto.ActiveParticipants = dto.Participants.Count(x =>
            string.Equals(x.ParticipantStatus, "Active", StringComparison.OrdinalIgnoreCase));
        dto.CompletedParticipants = dto.Participants.Count(x =>
            string.Equals(x.ParticipantStatus, "Completed", StringComparison.OrdinalIgnoreCase));

        return dto;
    }

    private void EnsureCurrentUserIsHr()
    {
        var roles = _currentUser.Roles ?? Array.Empty<string>();

        if (!roles.Any(x => x.Contains("hr", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("CurrentUserIsNotHr");
        }
    }

    private static Cycle? ResolveSelectedCycle(List<Cycle> cycles, Guid? cycleId)
    {
        if (cycles.Count == 0)
        {
            return null;
        }

        if (cycleId.HasValue)
        {
            var selected = cycles.FirstOrDefault(x => x.Id == cycleId.Value);
            if (selected is null)
            {
                throw new BusinessException("HrCycleNotFound");
            }

            return selected;
        }

        return cycles
            .OrderByDescending(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.CycleYear)
            .ThenByDescending(x => x.CreationTime)
            .First();
    }
}
