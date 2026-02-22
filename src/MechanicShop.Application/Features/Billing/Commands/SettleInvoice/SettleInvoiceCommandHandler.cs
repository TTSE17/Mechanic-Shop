using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Billing.Commands.SettleInvoice;

public class SettleInvoiceCommandHandler(
    ILogger<SettleInvoiceCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache,
    TimeProvider datetime) : IRequestHandler<SettleInvoiceCommand, Result<Success>>
{
    public async Task<Result<Success>> Handle(SettleInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await context.Invoices
            .FirstOrDefaultAsync(w => w.Id == command.InvoiceId, ct);

        if (invoice is null)
        {
            logger.LogWarning("Invoice {InvoiceId} not found.", command.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }

        var payInvoiceResult = invoice.MarkAsPaid(datetime);

        if (payInvoiceResult.IsError)
        {
            logger.LogWarning(
                "Invoice payment failed for InvoiceId: {InvoiceId}. Errors: {Errors}",
                invoice.Id,
                payInvoiceResult.Errors);

            return payInvoiceResult.Errors;
        }

        await context.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync("invoice", ct);

        logger.LogInformation("Invoice {InvoiceId} successfully paid.", invoice.Id);

        return Result.Success;
    }
}