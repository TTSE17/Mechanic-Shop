using MediatR;

namespace MechanicShop.Application.Common.Interfaces;

/*
 * This is a marker + configuration interface.
 * “This request supports caching, and here’s how.”
 * No logic. Just metadata.
 */
public interface ICachedQuery
{
    string CacheKey { get; }
    string[] Tags { get; }
    TimeSpan Expiration { get; }
}

/*
 * It is a MediatR request + supports caching.
 *
 * So any query implementing this interface:
    Automatically goes through CachingBehavior
    Automatically uses cache
 */
public interface ICachedQuery<out TResponse> : IRequest<TResponse>, ICachedQuery;