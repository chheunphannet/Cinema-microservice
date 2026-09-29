using Microsoft.AspNetCore.Http;
using Ticket.Api.Models;

namespace Ticket.Api.Services;

public interface ITicketIssuanceService
{
    Task<IResult> IssueTicketsAsync(IssueTicketsRequest request);
}

public interface ITicketPrintService
{
    Task<IResult> PrintTicketAsync(Guid ticketId, PrintTicketRequest? request, string? supervisorPinHeader);
}

public interface ITicketRedemptionService
{
    Task<IResult> RedeemTicketAsync(RedeemTicketRequest request);
}

public interface ITicketQueryService
{
    Task<IResult> GetTicketDetailsAsync(Guid ticketId);
    Task<IResult> GenerateETicketAsync(Guid ticketId);
    Task<IResult> GetSignedETicketPassAsync(Guid ticketId, long exp, string? sig);
    Task<IResult> GetSignedReservationPassAsync(Guid reservationId, long exp, string? sig);
}

