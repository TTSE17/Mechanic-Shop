using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

public class GenerateTokenQueryHandler(
    ILogger<GenerateTokenQueryHandler> logger,
    IIdentityService identityService,
    ITokenProvider tokenProvider) : IRequestHandler<GenerateTokenQuery, Result<TokenResponse>>
{
    public async Task<Result<TokenResponse>> Handle(GenerateTokenQuery query, CancellationToken ct)
    {
        var userResponse = await identityService.AuthenticateAsync(query.Email, query.Password);

        if (userResponse.IsError)
        {
            return userResponse.Errors;
        }

        var generateTokenResult = await tokenProvider.GenerateJwtTokenAsync(userResponse.Value, ct);

        if (!generateTokenResult.IsError) return generateTokenResult.Value;

        logger.LogError("Generate token error occurred: {ErrorDescription}", generateTokenResult.TopError.Description);

        return generateTokenResult.Errors;
    }
}