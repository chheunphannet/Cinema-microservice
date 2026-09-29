using System.Text.Json;
using Cinema.Foundation.Security;
using Microsoft.AspNetCore.Http;
using Ticket.Api.Models;
using Ticket.Api.Repositories;

namespace Ticket.Api.Services;

public class TicketPrintService : ITicketPrintService
{
    private readonly ITicketRepository _repository;

    public TicketPrintService(ITicketRepository repository)
    {
        _repository = repository;
    }

    public async Task<IResult> PrintTicketAsync(Guid ticketId, PrintTicketRequest? request, string? supervisorPinHeader)
    {
        var ticket = await _repository.GetTicketForPrintAsync(ticketId);

        if (ticket == null)
        {
            return Results.NotFound(new { error = $"Ticket {ticketId} not found." });
        }

        bool alreadyPrinted = (bool)ticket.is_printed;
        bool isReprint = request?.IsReprint ?? alreadyPrinted;
        var pin = request?.SupervisorPin ?? supervisorPinHeader;

        if (isReprint)
        {
            if (string.IsNullOrWhiteSpace(pin))
            {
                return Results.BadRequest(new { error = "Reprinting an already-printed ticket requires supervisor PIN authorization in body or X-Supervisor-Pin header." });
            }

            var supervisorHashes = await _repository.GetSupervisorPinHashesAsync();
            bool pinValid = supervisorHashes.Any(hash => PasswordHasher.Verify(pin, hash));
            
            if (!pinValid)
            {
                return Results.Json(new { error = "Invalid supervisor PIN for reprint authorization." }, statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        await _repository.MarkTicketPrintedAsync(ticketId);

        string action = isReprint ? "reprint" : "first_print";
        string metadata = JsonSerializer.Serialize(new { isReprint, timestamp = DateTimeOffset.UtcNow });
        
        await _repository.LogAuditAsync(ticketId, action, Constants.DefaultTerminalCode, metadata);

        return Results.Ok(new
        {
            ticketId,
            isPrinted = true,
            printedAt = DateTimeOffset.UtcNow,
            printFormat = "ESC/POS 80mm Direct Thermal Raw Bytes",
            drawerKick = false,
            message = isReprint ? "Reprint authorized by supervisor" : "First-time ticket stub printed successfully"
        });
    }
}
