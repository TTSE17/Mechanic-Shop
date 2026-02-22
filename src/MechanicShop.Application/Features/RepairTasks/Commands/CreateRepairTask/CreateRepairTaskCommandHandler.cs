using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Commands.CreateRepairTask;

public class CreateRepairTaskCommandHandler(
    ILogger<CreateRepairTaskCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache) : IRequestHandler<CreateRepairTaskCommand, Result<RepairTaskDto>>
{
    public async Task<Result<RepairTaskDto>> Handle(CreateRepairTaskCommand command, CancellationToken ct)
    {
        var nameExists = await context.RepairTasks
            .AnyAsync(p => EF.Functions.Like(p.Name, command.Name), ct);

        if (nameExists)
        {
            logger.LogWarning("Duplicate part name '{PartName}'.", command.Name);

            return RepairTaskErrors.DuplicateName;
        }

        List<Part> parts = [];

        foreach (var p in command.Parts)
        {
            var partResult = Part.Create(Guid.NewGuid(), p.Name, p.Cost, p.Quantity);

            if (partResult.IsError)
            {
                return partResult.Errors;
            }

            parts.Add(partResult.Value);
        }

        var createRepairTaskResult = RepairTask.Create(
            id: Guid.NewGuid(),
            name: command.Name!,
            laborCost: command.LaborCost,
            estimatedDurationInMins: command.EstimatedDurationInMins!.Value,
            parts: parts);

        if (createRepairTaskResult.IsError)
        {
            return createRepairTaskResult.Errors;
        }

        var repairTask = createRepairTaskResult.Value;

        context.RepairTasks.Add(repairTask);

        await context.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync("repair-task", ct);

        return repairTask.ToDto();
    }
}