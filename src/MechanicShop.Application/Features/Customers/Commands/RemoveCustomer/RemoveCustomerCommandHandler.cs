using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public class RemoveCustomerCommandHandler(
    ILogger<RemoveCustomerCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache) : IRequestHandler<RemoveCustomerCommand, Result<Deleted>>
{
    public async Task<Result<Deleted>> Handle(RemoveCustomerCommand command, CancellationToken ct)
    {
        var customer = await context.Customers.FindAsync([command.CustomerId], ct);

        if (customer is null)
        {
            logger.LogWarning("Customer with id {CustomerId} not found for deletion.", command.CustomerId);
            return ApplicationErrors.CustomerNotFound;
        }

        var hasAssociatedWorkOrders = await context.WorkOrders
            .Include(w => w.Vehicle)
            .Where(wo => wo.Vehicle != null)
            .AnyAsync(wo => wo.Vehicle!.CustomerId == command.CustomerId, ct);

        if (hasAssociatedWorkOrders)
        {
            logger.LogWarning(
                "Customer {CustomerId} cannot be deleted because they have associated work orders (past, scheduled, or in-progress).",
                command.CustomerId);

            return CustomerErrors.CannotDeleteCustomerWithWorkOrders;
        }

        context.Customers.Remove(customer);

        await context.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync("customer", ct);

        logger.LogInformation("Customer {CustomerId} deleted successfully.", command.CustomerId);

        return Result.Deleted;
    }
}