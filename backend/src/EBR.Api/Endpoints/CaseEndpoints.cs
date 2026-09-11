using System.Security.Claims;
using EBR.Application.Email;
using EBR.Domain.Identity;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Identity;
using EBR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EBR.Api.Endpoints;

public static partial class CaseEndpoints
{
    public static IEndpointRouteBuilder MapCaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/cases").WithTags("Expedientes")
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Administrator, SystemRoles.Coordinator));
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapGet("/{id:int}/history", HistoryAsync);
        group.MapGet("/{id:int}/assignments", AssignmentsAsync);
        group.MapGet("/{id:int}/schedules", ScheduleHistoryAsync);
        // Ruta estática: no colisiona con "/{id:int}" porque "schedule" no cumple la restricción de
        // tipo entero de esa plantilla.
        group.MapGet("/schedule", AgendaAsync);
        group.MapPost("/institutional", CreateInstitutionalAsync);
        group.MapPost("/{id:int}/transition", TransitionAsync);
        // RF-10: solo el Coordinador asigna o reasigna técnicos activos. El grupo ya admite
        // Administrador o Coordinador, así que aquí se agrega una segunda exigencia de rol para
        // restringir esta operación puntual solo a Coordinador (decisión conservadora: el SRS
        // atribuye la asignación de evaluador al Coordinador, no al Administrador).
        group.MapPost("/{id:int}/assign", AssignAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));
        // RF-07: mismo criterio de rol que la asignación de técnico — solo el Coordinador programa,
        // reprograma o cancela la agenda de evaluaciones.
        group.MapPost("/{id:int}/schedule", ScheduleAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));
        group.MapPost("/{id:int}/reschedule", RescheduleAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));
        group.MapPost("/{id:int}/cancel-schedule", CancelScheduleAsync)
            .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Coordinator));
        return endpoints;
    }

    private static async Task<IResult> ListAsync(EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.InspectionCases.AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(int id, EbrDbContext context, CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> HistoryAsync(int id, EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.CaseStateHistories.AsNoTracking().Where(item => item.CaseId == id)
            .OrderBy(item => item.ChangedAt).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateInstitutionalAsync(InstitutionalCaseRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 ||
            request.Observations?.Length > 2000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["case"] = ["El motivo admite 1-1000 caracteres y las observaciones hasta 2000."]
            });
        }

        if (!await context.Companies.AnyAsync(item => item.Id == request.CompanyId && item.IsActive, cancellationToken)) return Results.NotFound();

        var scheduling = new InstitutionalScheduling
        {
            CompanyId = request.CompanyId,
            Reason = request.Reason.Trim(),
            Observations = request.Observations?.Trim() ?? "",
            CreatedBy = userId
        };
        context.InstitutionalSchedulings.Add(scheduling);
        await context.SaveChangesAsync(cancellationToken);

        var inspectionCase = new InspectionCase
        {
            CompanyId = request.CompanyId,
            SourceType = "INSTITUTIONAL",
            SourceReferenceId = scheduling.Id,
            CreatedBy = userId
        };
        context.InspectionCases.Add(inspectionCase);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/cases/{inspectionCase.Id}", inspectionCase);
    }

    private static async Task<IResult> TransitionAsync(int id, TransitionRequest request, ClaimsPrincipal principal, EbrDbContext context, CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.NewStatus) || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return Validation("transition", "El estado y el motivo son obligatorios; el motivo admite hasta 1000 caracteres.");
        var target = request.NewStatus.Trim().ToUpperInvariant();
        if (target != CaseStatuses.Cancelled)
            return Results.Conflict(new
            {
                message = "La transición solicitada pertenece a un flujo operativo y debe ejecutarse mediante su endpoint especializado."
            });
        if (!CaseStateMachine.CanTransition(item.Status, target))
            return Results.Conflict(new { message = $"No se permite cambiar de {item.Status} a {target}." });
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
        var previous = item.Status;

        var currentSchedules = await context.CaseSchedules
            .Where(value => value.CaseId == id && value.IsCurrent).ToListAsync(cancellationToken);
        foreach (var schedule in currentSchedules)
        {
            schedule.IsCurrent = false;
            schedule.CancelledAt = DateTimeOffset.UtcNow;
            schedule.CancelledBy = userId;
            schedule.CancellationReason = request.Reason.Trim();
        }

        var currentAssignments = await context.CaseAssignments
            .Where(value => value.CaseId == id && value.IsCurrent).ToListAsync(cancellationToken);
        foreach (var assignment in currentAssignments) assignment.IsCurrent = false;

        item.Status = target;
        context.CaseStateHistories.Add(new CaseStateHistory
        {
            CaseId = item.Id,
            PreviousStatus = previous,
            NewStatus = target,
            Reason = request.Reason.Trim(),
            ChangedBy = userId
        });
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private static async Task<IResult> AssignmentsAsync(int id, EbrDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.InspectionCases.AnyAsync(item => item.Id == id, cancellationToken)) return Results.NotFound();
        return Results.Ok(await context.CaseAssignments.AsNoTracking().Where(item => item.CaseId == id)
            .OrderBy(item => item.AssignedAt).ThenBy(item => item.Id).ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Asigna o reasigna el técnico evaluador de un caso (RF-10). PostgreSQL ejecuta
    /// <c>sp_asignar_tecnico</c>, que concentra bloqueo, autorización e historial; el proveedor en
    /// memoria aplica el comportamiento equivalente mediante EF Core para permitir pruebas aisladas.
    /// </summary>
    private static async Task<IResult> AssignAsync(
        int id,
        AssignTechnicianRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = ["El motivo es obligatorio y admite hasta 1000 caracteres."]
            });
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var technician = await userManager.FindByIdAsync(request.TechnicianId.ToString());
        if (technician is null ||
            technician.ApprovalStatus != UserApprovalStatus.Approved ||
            !await userManager.IsInRoleAsync(technician, SystemRoles.Evaluator))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["technicianId"] = ["El técnico debe tener el rol Técnico Evaluador y estar activo (aprobado)."]
            });
        }

        var isFirstAssignment = item.Status == CaseStatuses.PendingAssignment;
        if (isFirstAssignment)
        {
            if (!CaseStateMachine.CanTransition(item.Status, CaseStatuses.Assigned))
                return Results.Conflict(new { message = $"No se permite asignar un caso en estado {item.Status}." });
        }
        else if (!CaseStateMachine.CanReassign(item.Status))
        {
            return Results.Conflict(new { message = $"No se permite reasignar un caso en estado {item.Status}." });
        }

        var reason = request.Reason.Trim();
        var existingCurrent = await context.CaseAssignments
            .SingleOrDefaultAsync(assignment => assignment.CaseId == id && assignment.IsCurrent, cancellationToken);
        if (existingCurrent?.TechnicianId == technician.Id) return Results.Ok(existingCurrent);

        if (context.Database.IsNpgsql())
        {
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_asignar_tecnico({id}, {technician.Id}, {actingUserId}, {reason})", cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            var storedAssignment = await context.CaseAssignments.AsNoTracking()
                .SingleAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
            await AddNotificationAsync(context, emailSender, logger,technician.Id, NotificationTypes.CaseAssigned,
                "Nuevo expediente asignado", $"Se le asignó el expediente {id}.", id,
                $"assignment:{storedAssignment.Id}:{technician.Id}", cancellationToken);
            return Results.Ok(storedAssignment);
        }

        if (isFirstAssignment)
        {
            var previousStatus = item.Status;
            item.Status = CaseStatuses.Assigned;
            context.CaseStateHistories.Add(new CaseStateHistory
            {
                CaseId = item.Id,
                PreviousStatus = previousStatus,
                NewStatus = CaseStatuses.Assigned,
                Reason = reason,
                ChangedBy = actingUserId
            });
        }
        else
        {
            var currentAssignments = await context.CaseAssignments
                .Where(assignment => assignment.CaseId == id && assignment.IsCurrent)
                .ToListAsync(cancellationToken);
            foreach (var previous in currentAssignments) previous.IsCurrent = false;

            if (item.Status == CaseStatuses.Scheduled)
            {
                var schedule = await context.CaseSchedules
                    .SingleOrDefaultAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
                if (schedule is not null)
                {
                    schedule.IsCurrent = false;
                    schedule.CancelledAt = DateTimeOffset.UtcNow;
                    schedule.CancelledBy = actingUserId;
                    schedule.CancellationReason = "Cancelada automáticamente por reasignación de técnico.";
                }
                item.Status = CaseStatuses.Assigned;
                context.CaseStateHistories.Add(new CaseStateHistory
                {
                    CaseId = item.Id,
                    PreviousStatus = CaseStatuses.Scheduled,
                    NewStatus = CaseStatuses.Assigned,
                    Reason = reason,
                    ChangedBy = actingUserId
                });
            }
        }

        var assignment = new CaseAssignment
        {
            CaseId = id,
            TechnicianId = technician.Id,
            IsCurrent = true,
            AssignedBy = actingUserId,
            Reason = reason
        };
        context.CaseAssignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);
        await AddNotificationAsync(context, emailSender, logger,technician.Id, NotificationTypes.CaseAssigned,
            "Nuevo expediente asignado", $"Se le asignó el expediente {id}.", id,
            $"assignment:{assignment.Id}:{technician.Id}", cancellationToken);
        return Results.Ok(assignment);
    }

    private static async Task<IResult> ScheduleHistoryAsync(int id, EbrDbContext context, CancellationToken cancellationToken) =>
        Results.Ok(await context.CaseSchedules.AsNoTracking().Where(item => item.CaseId == id)
            .OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken));

    /// <summary>
    /// Programa por primera vez la evaluación de un caso (RF-07). Exige un técnico ya asignado
    /// (<see cref="CaseAssignment"/> vigente), transiciona <c>ASSIGNED -&gt; SCHEDULED</c> con
    /// <see cref="CaseStateMachine"/> y valida que el técnico no tenga otra programación vigente que
    /// se solape en el tiempo (ver <see cref="CaseScheduleWindow"/>).
    /// </summary>
    private static async Task<IResult> ScheduleAsync(
        int id,
        ScheduleCaseRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();

        if (!ValidScheduleRequest(request))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["schedule"] = ["La fecha es obligatoria y no puede estar más de cinco minutos en el pasado; el motivo admite 1-1000 caracteres, la prioridad hasta 20 y las observaciones hasta 2000."]
            });
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
        if (assignment is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["technicianId"] = ["El caso debe tener un técnico asignado antes de programarse."]
            });
        }
        if (!await IsApprovedEvaluatorAsync(userManager, assignment.TechnicianId))
            return Validation("technicianId", "El técnico asignado debe seguir aprobado y tener rol Técnico Evaluador.");

        if (!CaseStateMachine.CanTransition(item.Status, CaseStatuses.Scheduled))
            return Results.Conflict(new { message = $"No se permite programar un caso en estado {item.Status}." });

        if (await HasOverlapAsync(context, assignment.TechnicianId, request.ScheduledFor, id, cancellationToken))
        {
            return Results.Conflict(new
            {
                message = "El técnico ya tiene otra programación vigente que se solapa con ese horario."
            });
        }

        if (context.Database.IsNpgsql())
        {
            var requestedPriority = request.Priority?.Trim().ToUpperInvariant() ?? "";
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_programar_evaluacion({id}, {"PROGRAMAR"}, {request.ScheduledFor}, {requestedPriority}, {request.Reason.Trim()}, {request.Observations?.Trim() ?? ""}, {actingUserId})",
                cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            var storedSchedule = await context.CaseSchedules.AsNoTracking()
                .SingleAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
            await AddNotificationAsync(context, emailSender, logger,assignment.TechnicianId, NotificationTypes.CaseScheduled,
                "Evaluación programada", $"El expediente {id} fue programado para {storedSchedule.ScheduledFor:u}.", id,
                $"schedule:{storedSchedule.Id}:{assignment.TechnicianId}", cancellationToken);
            return Results.Ok(storedSchedule);
        }

        var previousStatus = item.Status;
        item.Status = CaseStatuses.Scheduled;
        var priority = string.IsNullOrWhiteSpace(request.Priority) ? item.Priority : request.Priority.Trim().ToUpperInvariant();
        item.Priority = priority;
        context.CaseStateHistories.Add(new CaseStateHistory
        {
            CaseId = item.Id,
            PreviousStatus = previousStatus,
            NewStatus = CaseStatuses.Scheduled,
            Reason = request.Reason.Trim(),
            ChangedBy = actingUserId
        });

        var schedule = new CaseSchedule
        {
            CaseId = id,
            TechnicianId = assignment.TechnicianId,
            ScheduledFor = request.ScheduledFor,
            Priority = priority,
            Reason = request.Reason.Trim(),
            Observations = request.Observations?.Trim() ?? "",
            IsCurrent = true,
            ScheduledBy = actingUserId
        };
        context.CaseSchedules.Add(schedule);
        await context.SaveChangesAsync(cancellationToken);
        await AddNotificationAsync(context, emailSender, logger,assignment.TechnicianId, NotificationTypes.CaseScheduled,
            "Evaluación programada", $"El expediente {id} fue programado para {schedule.ScheduledFor:u}.", id,
            $"schedule:{schedule.Id}:{assignment.TechnicianId}", cancellationToken);
        return Results.Ok(schedule);
    }

    /// <summary>
    /// Reprograma un caso que ya tiene una programación vigente (RF-07): marca la anterior como
    /// histórica y crea la nueva, validando de nuevo el solapamiento. No cambia el estado del caso
    /// — usa el mismo criterio de estados activos que <see cref="CaseStateMachine.CanReassign"/>
    /// (mientras el expediente sigue activo y el técnico todavía tiene trabajo pendiente sobre él),
    /// igual que la reasignación de técnico.
    /// </summary>
    private static async Task<IResult> RescheduleAsync(
        int id,
        ScheduleCaseRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();

        if (!ValidScheduleRequest(request))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["schedule"] = ["La fecha es obligatoria y no puede estar más de cinco minutos en el pasado; el motivo admite 1-1000 caracteres, la prioridad hasta 20 y las observaciones hasta 2000."]
            });
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        if (!CaseStateMachine.CanReassign(item.Status))
            return Results.Conflict(new { message = $"No se permite reprogramar un caso en estado {item.Status}." });

        var current = await context.CaseSchedules.SingleOrDefaultAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
        if (current is null)
            return Results.Conflict(new { message = "El caso no tiene una programación vigente para reprogramar." });

        var assignment = await context.CaseAssignments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
        if (assignment is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["technicianId"] = ["El caso debe tener un técnico asignado para reprogramarse."]
            });
        }
        if (!await IsApprovedEvaluatorAsync(userManager, assignment.TechnicianId))
            return Validation("technicianId", "El técnico asignado debe seguir aprobado y tener rol Técnico Evaluador.");

        if (await HasOverlapAsync(context, assignment.TechnicianId, request.ScheduledFor, id, cancellationToken))
        {
            return Results.Conflict(new
            {
                message = "El técnico ya tiene otra programación vigente que se solapa con ese horario."
            });
        }

        if (context.Database.IsNpgsql())
        {
            var requestedPriority = request.Priority?.Trim().ToUpperInvariant() ?? "";
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_programar_evaluacion({id}, {"REPROGRAMAR"}, {request.ScheduledFor}, {requestedPriority}, {request.Reason.Trim()}, {request.Observations?.Trim() ?? ""}, {actingUserId})",
                cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            return Results.Ok(await context.CaseSchedules.AsNoTracking()
                .SingleAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken));
        }

        current.IsCurrent = false;
        var priority = string.IsNullOrWhiteSpace(request.Priority) ? item.Priority : request.Priority.Trim().ToUpperInvariant();
        item.Priority = priority;

        var schedule = new CaseSchedule
        {
            CaseId = id,
            TechnicianId = assignment.TechnicianId,
            ScheduledFor = request.ScheduledFor,
            Priority = priority,
            Reason = request.Reason.Trim(),
            Observations = request.Observations?.Trim() ?? "",
            IsCurrent = true,
            ScheduledBy = actingUserId
        };
        context.CaseSchedules.Add(schedule);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(schedule);
    }

    /// <summary>
    /// Cancela la programación vigente de un caso (RF-07). No transiciona el estado del caso: la
    /// cancelación de la fecha programada es una decisión distinta de cancelar el expediente
    /// completo (para eso ya existe <c>POST /api/cases/{id}/transition</c> hacia <c>CANCELLED</c>).
    /// Deja constancia de quién y cuándo canceló en la misma fila y no permite cancelar dos veces:
    /// si no hay programación vigente, responde <c>409</c>.
    /// </summary>
    private static async Task<IResult> CancelScheduleAsync(
        int id,
        CancelScheduleRequest request,
        ClaimsPrincipal principal,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        var item = await context.InspectionCases.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = ["El motivo es obligatorio y admite hasta 1000 caracteres."]
            });
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId)) return Results.Unauthorized();

        var current = await context.CaseSchedules.SingleOrDefaultAsync(value => value.CaseId == id && value.IsCurrent, cancellationToken);
        if (current is null)
            return Results.Conflict(new { message = "El caso no tiene una programación vigente para cancelar." });

        if (context.Database.IsNpgsql())
        {
            var problem = await PostgresCommandProblem.ExecuteAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL sp_programar_evaluacion({id}, {"CANCELAR"}, {DateTimeOffset.UtcNow}, {""}, {request.Reason.Trim()}, {""}, {actingUserId})",
                cancellationToken));
            if (problem is not null) return problem;
            context.ChangeTracker.Clear();
            return Results.Ok(await context.CaseSchedules.AsNoTracking()
                .Where(value => value.CaseId == id).OrderByDescending(value => value.Id).FirstAsync(cancellationToken));
        }

        current.IsCurrent = false;
        current.CancelledAt = DateTimeOffset.UtcNow;
        current.CancelledBy = actingUserId;
        current.CancellationReason = request.Reason.Trim();
        if (item.Status == CaseStatuses.Scheduled)
        {
            item.Status = CaseStatuses.Assigned;
            context.CaseStateHistories.Add(new CaseStateHistory
            {
                CaseId = item.Id,
                PreviousStatus = CaseStatuses.Scheduled,
                NewStatus = CaseStatuses.Assigned,
                Reason = request.Reason.Trim(),
                ChangedBy = actingUserId
            });
        }
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(current);
    }

    /// <summary>
    /// Agenda de evaluaciones (RF-11) en un rango de fechas: alcanza para las vistas de día, semana o
    /// mes según el rango que envíe el cliente en <paramref name="from"/>/<paramref name="to"/>.
    /// Devuelve solo programaciones vigentes, con empresa, dirección, fecha y estado del caso.
    /// </summary>
    private static async Task<IResult> AgendaAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        EbrDbContext context,
        CancellationToken cancellationToken)
    {
        if (from is not null && to is not null && from > to)
            return Validation("range", "El inicio del rango no puede ser posterior al final.");
        var query = context.CaseSchedules.AsNoTracking().Where(value => value.IsCurrent);
        if (from is not null) query = query.Where(value => value.ScheduledFor >= from);
        if (to is not null) query = query.Where(value => value.ScheduledFor < to);

        var schedules = await query.OrderBy(value => value.ScheduledFor).ToListAsync(cancellationToken);
        if (schedules.Count == 0) return Results.Ok(Array.Empty<AgendaEntry>());

        var caseIds = schedules.Select(value => value.CaseId).Distinct().ToList();
        var cases = await context.InspectionCases.AsNoTracking()
            .Where(value => caseIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var companyIds = cases.Values.Select(value => value.CompanyId).Distinct().ToList();
        var companies = await context.Companies.AsNoTracking()
            .Where(value => companyIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        var entries = schedules.Select(schedule =>
        {
            var inspectionCase = cases[schedule.CaseId];
            var company = companies[inspectionCase.CompanyId];
            return new AgendaEntry(
                schedule.CaseId,
                company.Id,
                company.LegalName,
                company.Address,
                schedule.ScheduledFor,
                inspectionCase.Status,
                schedule.Priority,
                schedule.TechnicianId);
        }).ToList();

        return Results.Ok(entries);
    }

    /// <summary>
    /// Dos programaciones vigentes del mismo técnico no pueden solaparse en el tiempo (RF-07). El
    /// candidato se materializa filtrado por técnico y vigencia (traducible sin problema por
    /// cualquier proveedor de EF Core) y la aritmética de solapamiento se resuelve en memoria para
    /// evitar depender de la traducción de <see cref="DateTimeOffset.AddHours"/> del proveedor.
    /// </summary>
    private static async Task<bool> HasOverlapAsync(
        EbrDbContext context,
        Guid technicianId,
        DateTimeOffset scheduledFor,
        int excludedCaseId,
        CancellationToken cancellationToken)
    {
        var candidates = await context.CaseSchedules.AsNoTracking()
            .Where(value => value.IsCurrent && value.TechnicianId == technicianId && value.CaseId != excludedCaseId)
            .ToListAsync(cancellationToken);
        var newEnd = scheduledFor.AddHours(CaseScheduleWindow.HoursPerEvaluation);
        return candidates.Any(value =>
            scheduledFor < value.ScheduledFor.AddHours(CaseScheduleWindow.HoursPerEvaluation) && value.ScheduledFor < newEnd);
    }

    private static async Task<bool> IsApprovedEvaluatorAsync(UserManager<ApplicationUser> userManager, Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && user.ApprovalStatus == UserApprovalStatus.Approved &&
               await userManager.IsInRoleAsync(user, SystemRoles.Evaluator);
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static bool ValidScheduleRequest(ScheduleCaseRequest request) =>
        request.ScheduledFor != default && request.ScheduledFor >= DateTimeOffset.UtcNow.AddMinutes(-5) &&
        !string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Length <= 1000 &&
        (request.Priority is null || request.Priority.Length <= 20) &&
        (request.Observations is null || request.Observations.Length <= 2000);

    private static async Task AddNotificationAsync(
        EbrDbContext context, IEmailSender emailSender, ILogger<Program> logger, Guid recipientId, string type,
        string title, string message, int caseId, string operationId, CancellationToken cancellationToken)
    {
        if (await context.Notifications.AnyAsync(x => x.OperationId == operationId, cancellationToken)) return;
        context.Notifications.Add(new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceType = "CASE",
            ReferenceId = caseId,
            OperationId = operationId
        });
        await context.SaveChangesAsync(cancellationToken);
        await SendNotificationEmailAsync(context, emailSender, logger, recipientId, title, message, cancellationToken);
    }

    // El correo es un complemento de la notificación in-app, nunca un requisito para que el flujo
    // avance: un proveedor SMTP caído no puede bloquear la asignación o programación de un caso.
    internal static async Task SendNotificationEmailAsync(
        EbrDbContext context, IEmailSender emailSender, ILogger<Program> logger, Guid recipientId,
        string title, string message, CancellationToken cancellationToken)
    {
        var recipientEmail = await context.Users.AsNoTracking()
            .Where(user => user.Id == recipientId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            await emailSender.SendAsync(recipientEmail, title, message, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNotificationEmailFailed(logger, recipientEmail, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible enviar el correo de notificación a {RecipientEmail}.")]
    private static partial void LogNotificationEmailFailed(ILogger logger, string recipientEmail, Exception ex);

    private sealed record TransitionRequest(string NewStatus, string Reason);
    private sealed record InstitutionalCaseRequest(int CompanyId, string Reason, string? Observations = null);
    private sealed record AssignTechnicianRequest(Guid TechnicianId, string Reason);
    private sealed record ScheduleCaseRequest(DateTimeOffset ScheduledFor, string Reason, string? Priority = null, string? Observations = null);
    private sealed record CancelScheduleRequest(string Reason);

    private sealed record AgendaEntry(
        int CaseId,
        int CompanyId,
        string CompanyName,
        string CompanyAddress,
        DateTimeOffset ScheduledFor,
        string Status,
        string Priority,
        Guid TechnicianId);
}
