using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;

public class GetRepairTaskByIdQueryHandler(ILogger<GetRepairTaskByIdQueryHandler> logger, IAppDbContext context)
    : IRequestHandler<GetRepairTaskByIdQuery, Result<RepairTaskDto>>
{
    public async Task<Result<RepairTaskDto>> Handle(GetRepairTaskByIdQuery query, CancellationToken ct)
    {
        var repairTask = await context.RepairTasks
            .AsNoTracking()
            .Include(c => c.Parts)
            .FirstOrDefaultAsync(c => c.Id == query.RepairTaskId, ct);

        if (repairTask is not null) return repairTask.ToDto();

        logger.LogWarning("Repair task with id {RepairTaskId} was not found", query.RepairTaskId);

        return ApplicationErrors.RepairTaskNotFound;
    }
}