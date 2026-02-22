using MediatR;

namespace MechanicShop.Application.Common.Behaviours;

using FluentValidation;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Common.Results.Abstractions;

/*
    Controller
       ↓
    Pipeline behaviors (Validation, Logging, Caching, Transactions, etc.)
       ↓
    Request Handler (Command / Query Handler)

    Before executing any MediatR handler, check if a FluentValidation validator exists.

 */

public class ValidationBehavior<TRequest, TResponse>(IValidator<TRequest>? validator = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (validator is null)
        {
            return await next(ct);
        }

        var validationResult = await validator.ValidateAsync(request, ct); // This comes from FluentValidation.

        if (validationResult.IsValid)
        {
            return await next(ct);
        }

        var errors = validationResult.Errors
            .ConvertAll(error => Error.Validation(
                code: error.PropertyName,
                description: error.ErrorMessage));

        return (dynamic)errors;
    }
}