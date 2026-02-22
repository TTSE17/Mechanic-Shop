using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public class CreateWorkOrderCommandHandler(
    ILogger<CreateWorkOrderCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache,
    IWorkOrderPolicy workOrderValidator) : IRequestHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    public async Task<Result<WorkOrderDto>> Handle(CreateWorkOrderCommand command, CancellationToken ct)
    {
        var repairTasks = await context.RepairTasks
            .Where(t => command.RepairTaskIds.Contains(t.Id))
            .ToListAsync(ct);

        if (repairTasks.Count != command.RepairTaskIds.Count)
        {
            var missingIds = command.RepairTaskIds.Except(repairTasks.Select(t => t.Id)).ToArray();

            logger.LogError("Some RepairTaskIds not found: {MissingIds}", string.Join(", ", missingIds));

            return ApplicationErrors.RepairTaskNotFound;
        }

        var totalEstimatedDuration = TimeSpan.FromMinutes(repairTasks.Sum(r => (int)r.EstimatedDurationInMins));
        var endAt = command.StartAt.Add(totalEstimatedDuration);

        if (workOrderValidator.IsOutsideOperatingHours(command.StartAt, totalEstimatedDuration))
        {
            logger.LogError("The WorkOrder time ({StartAt} ? {EndAt}) is outside of store operating hours.",
                command.StartAt, endAt);

            return ApplicationErrors.WorkOrderOutsideOperatingHour(command.StartAt, endAt);
        }

        var checkMinRequirementResult = workOrderValidator.ValidateMinimumRequirement(command.StartAt, endAt);

        if (checkMinRequirementResult.IsError)
        {
            logger.LogError("WorkOrder duration is shorter than the configured minimum.");

            return checkMinRequirementResult.Errors;
        }

        var checkSpotAvailabilityResult = await workOrderValidator.CheckSpotAvailabilityAsync(
            command.Spot,
            command.StartAt,
            endAt,
            excludeWorkOrderId: null,
            ct);

        if (checkSpotAvailabilityResult.IsError)
        {
            logger.LogError("Spot: {Spot} is not available.", command.Spot.ToString());
            return checkSpotAvailabilityResult.Errors;
        }

        var vehicle = await context.Vehicles
            .Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken: ct);

        if (vehicle is null)
        {
            logger.LogError("Vehicle with Id '{VehicleId}' does not exist.", command.VehicleId);

            return ApplicationErrors.VehicleNotFound;
        }

        var labor = await context.Employees.FindAsync([command.LaborId], ct);

        if (labor is null)
        {
            logger.LogError("Invalid LaborId: {LaborId}", command.LaborId.ToString());
            return ApplicationErrors.LaborNotFound;
        }

        var hasVehicleConflict = await context.WorkOrders.AnyAsync
        (
            a =>
                a.VehicleId == command.VehicleId &&
                a.StartAtUtc.Date == command.StartAt.Date &&
                a.StartAtUtc < endAt &&
                a.EndAtUtc > command.StartAt,
            ct
        );

        if (hasVehicleConflict)
        {
            logger.LogError("Vehicle with Id '{VehicleId}' already has an overlapping WorkOrder.", command.VehicleId);
            return Error.Conflict(
                code: "Vehicle_Overlapping_WorkOrders",
                description: "The vehicle already has an overlapping WorkOrder.");
        }

        var isLaborOccupied = await context.WorkOrders.AnyAsync
        (
            a =>
                a.LaborId == command.LaborId &&
                a.StartAtUtc < endAt &&
                a.EndAtUtc > command.StartAt,
            ct
        );

        if (isLaborOccupied)
        {
            logger.LogError("Labor with Id '{LaborId}' is already occupied during the requested time.",
                command.LaborId);
            return Error.Conflict(
                code: "Labor_Occupied",
                description: "Labor is already occupied during the requested time.");
        }

        var createWorkOrderResult = WorkOrder.Create
        (
            Guid.NewGuid(),
            command.VehicleId,
            command.StartAt,
            endAt,
            command.LaborId!.Value,
            command.Spot,
            repairTasks
        );

        if (createWorkOrderResult.IsError)
        {
            logger.LogError("Failed to create WorkOrder: {Error}", createWorkOrderResult.TopError.Description);

            return createWorkOrderResult.Errors;
        }

        var workOrder = createWorkOrderResult.Value;

        context.WorkOrders.Add(workOrder);

        workOrder.AddDomainEvent(new WorkOrderCollectionModified());

        await context.SaveChangesAsync(ct);

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        logger.LogInformation("WorkOrder with Id '{WorkOrderId}' created successfully.", workOrder.Id);

        await cache.RemoveByTagAsync("work-order", ct);

        return workOrder.ToDto();
    }
}