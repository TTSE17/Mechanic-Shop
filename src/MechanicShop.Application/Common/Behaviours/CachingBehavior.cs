using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results.Abstractions;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Common.Behaviours;

/*
 * CachingBehavior = centralized cache logic.
 * I want to intercept every MediatR request.
 *
    Controller
       ↓
    Pipeline behaviors (Validation, Logging, Caching, Transactions, etc.)
       ↓
    Request Handler (Command / Query Handler)
 */
public class CachingBehavior<TRequest, TResponse>(
    HybridCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICachedQuery cachedRequest)
        {
            return await next(ct);
        }

        logger.LogInformation("Checking cache for {RequestName}", typeof(TRequest).Name);

        var result = await cache.GetOrCreateAsync<TResponse>
            (
                cachedRequest.CacheKey,
                _ => new ValueTask<TResponse>((TResponse)(object)null!),
                new HybridCacheEntryOptions
                {
                    Flags = HybridCacheEntryFlags.DisableUnderlyingData
                },
                cancellationToken: ct
            )
            ;

        if (result is null)
        {
            result = await next(ct);

            // Only successful results are cached.
            if (result is IResult { IsSuccess: true })
            {
                logger.LogInformation("Caching result for {RequestName}", typeof(TRequest).Name);

                await cache.SetAsync
                (
                    cachedRequest.CacheKey,
                    result,
                    new HybridCacheEntryOptions
                    {
                        Expiration = cachedRequest.Expiration
                    },
                    cachedRequest.Tags,
                    ct
                );
            }
        }

        return result;
    }
}