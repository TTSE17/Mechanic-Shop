using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

/*
 * MediatR knows:
    Request type: CreateCustomerCommand
    Handler: CreateCustomerCommandHandler
    Response type: Result<CustomerDto>
 */
public class CreateCustomerCommandHandler(
    ILogger<CreateCustomerCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache) : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLower();

        var exists = await context.Customers.AnyAsync(c => c.Email!.ToLower() == email, ct);

        if (exists)
        {
            logger.LogWarning("Customer creation aborted. Email already exists.");

            return CustomerErrors.CustomerExists;
        }

        List<Vehicle> vehicles = [];

        foreach (var v in command.Vehicles)
        {
            var vehicleResult = Vehicle.Create(Guid.NewGuid(), v.Make, v.Model, v.Year, v.LicensePlate);

            if (vehicleResult.IsError)
            {
                return vehicleResult.Errors;
            }

            vehicles.Add(vehicleResult.Value);
        }

        var createCustomerResult = Customer.Create(
            Guid.NewGuid(),
            command.Name.Trim(),
            command.PhoneNumber.Trim(),
            command.Email.Trim(),
            vehicles);

        if (createCustomerResult.IsError)
        {
            return createCustomerResult.Errors;
        }

        context.Customers.Add(createCustomerResult.Value);

        await context.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync("customer", ct);

        var customer = createCustomerResult.Value;

        logger.LogInformation("Customer created successfully. Id: {CustomerId}", createCustomerResult.Value.Id);

        return customer.ToDto();
    }
}