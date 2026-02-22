using System.Security.Claims;

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Identity.Policies;

// (A marker object) This policy requires labor assignment verification.
public class LaborAssignedRequirement : IAuthorizationRequirement;

// ASP.NET Core will automatically call it when a policy that uses LaborAssignedRequirement
public class LaborAssignedHandler(IAppDbContext context, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<LaborAssignedRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context1,
        LaborAssignedRequirement requirement)
    {
        var userId = context1.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context1.Fail();
            return;
        }

        // Extract WorkOrderId dynamically from the route
        var workOrderIdString = httpContextAccessor.HttpContext?.Request.RouteValues["WorkOrderId"]?.ToString();

        if (!Guid.TryParse(workOrderIdString, out var workOrderId))
        {
            context1.Fail();
            return;
        }

        var isAssigned = await context.WorkOrders
            .AnyAsync(a => a.Id == workOrderId && a.LaborId == Guid.Parse(userId));

        if (isAssigned)
        {
            context1.Succeed(requirement);
            return;
        }

        if (context1.User.IsInRole(nameof(Role.Manager)))
        {
            context1.Succeed(requirement);
            return;
        }

        context1.Fail();
    }
}