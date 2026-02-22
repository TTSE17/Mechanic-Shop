using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Labors.Mappers;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Application.Features.Scheduling.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Workorders.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery;

public class GetDailyScheduleQueryHandler(IAppDbContext context, TimeProvider datetime)
    : IRequestHandler<GetDailyScheduleQuery, Result<ScheduleDto>>
{
    public async Task<Result<ScheduleDto>> Handle(GetDailyScheduleQuery query, CancellationToken ct)
    {
        var localStart = query.ScheduleDate.ToDateTime(TimeOnly.MinValue); // ex: 2026-01-07 => 2026-01-07 00:00

        var localEnd = localStart.AddDays(1);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, query.TimeZone);

        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, query.TimeZone);

        // Get only WorkOrders that overlap the requested day.
        var workOrders = await context.WorkOrders
            .Where(w =>
                w.StartAtUtc < utcEnd &&
                w.EndAtUtc > utcStart &&
                (query.LaborId == null || w.LaborId == query.LaborId))
            .Include(w => w.RepairTasks)
            .Include(w => w.Vehicle)
            .Include(w => w.Labor)
            .ToListAsync(ct);

        var now = TimeZoneInfo.ConvertTime(datetime.GetUtcNow(), query.TimeZone);

        var result = new ScheduleDto
        {
            OnDate = query.ScheduleDate,
            EndOfDay = localEnd < now, // true => if the entire day is already in the past.
            Spots = []
        };

        foreach (var spot in Enum.GetValues<Spot>())
        {
            var current = localStart;

            var slots = new List<AvailabilitySlotDto>();

            // per spot.
            var woBySpot = workOrders
                .Where(w => w.Spot == spot)
                .OrderBy(w => w.StartAtUtc)
                .ToList();

            while (current < localEnd)
            {
                // Day is divided into 15-minute slots.
                var next = current.AddMinutes(15);

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(current, query.TimeZone);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(next, query.TimeZone);

                // per slot.
                var wo = woBySpot.FirstOrDefault(w => w.StartAtUtc < endUtc && w.EndAtUtc > startUtc);

                if (wo != null) // Slot is occupied
                {
                    if (slots.All(s => s.WorkOrderId != wo.Id))
                    {
                        slots.Add(new AvailabilitySlotDto
                        {
                            WorkOrderId = wo.Id,
                            Spot = spot,
                            StartAt = wo.StartAtUtc,
                            EndAt = wo.EndAtUtc,
                            Vehicle = FormatVehicleInfo(wo.Vehicle!),
                            Labor = wo.Labor!.ToDto(),
                            IsOccupied = true,
                            RepairTasks = [.. wo.RepairTasks.ToList().ConvertAll(rt => rt.ToDto())],
                            WorkOrderLocked = !wo.IsEditable,
                            State = wo.State,
                            IsAvailable = false
                        });
                    }
                }

                else // Slot is free
                {
                    slots.Add(new AvailabilitySlotDto
                    {
                        Spot = spot,
                        StartAt = startUtc,
                        EndAt = endUtc,
                        WorkOrderLocked = false,
                        IsAvailable = current >= now
                    });
                }

                current = next;
            }

            result.Spots.Add(new SpotDto
            {
                Spot = spot,
                Slots = slots
            });
        }

        return result;
    }

    private static string FormatVehicleInfo(Vehicle vehicle) => $"{vehicle.Make} | {vehicle.LicensePlate}";
}