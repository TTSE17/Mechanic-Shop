using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Commands.RemoveRepairTask;

public class RemoveRepairTaskCommandHandler(
    ILogger<RemoveRepairTaskCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache) : IRequestHandler<RemoveRepairTaskCommand, Result<Deleted>>
{
    public async Task<Result<Deleted>> Handle(RemoveRepairTaskCommand command, CancellationToken ct)
    {
        var repairTask = await context.RepairTasks
            .FindAsync([command.RepairTaskId], ct);

        if (repairTask is null)
        {
            logger.LogWarning("RepairTask {RepairTaskId} not found for deletion.", command.RepairTaskId);
            return ApplicationErrors.RepairTaskNotFound;
        }

        var isInUse = await context.WorkOrders.AsNoTracking()
            .SelectMany(x => x.RepairTasks)
            .AnyAsync(rt => rt.Id == command.RepairTaskId, ct);

        if (isInUse)
        {
            logger.LogWarning("RepairTask {RepairTaskId} cannot be deleted — in use by work orders.",
                command.RepairTaskId);

            return RepairTaskErrors.InUse;
        }

        context.RepairTasks.Remove(repairTask);
        await context.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync("repair-task", ct);

        logger.LogInformation("RepairTask {RepairTaskId} deleted successfully.", command.RepairTaskId);

        return Result.Deleted;
    }
}