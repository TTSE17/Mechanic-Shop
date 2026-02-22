using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

/*
 * IRequest : This object represents a request (command or query) that will be sent through MediatR.
 * Mark this record as a message that MediatR (works as a message dispatcher) can dispatch to a handler.
 *
 * Because without IRequest:
    ❌ MediatR cannot discover handlers.
    ❌ No compile-time guarantee of response type.
    ❌ You lose pipeline behaviors (validation, logging, transactions, etc.)
 */ 

public sealed record CreateCustomerCommand(
    string Name,
    string PhoneNumber,
    string Email,
    List<CreateVehicleCommand> Vehicles) : IRequest<Result<CustomerDto>>;