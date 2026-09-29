using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Ticket.Api.Models;
using Ticket.Api.Repositories;

namespace Ticket.Api.Services;

public class TicketIssuanceService : ITicketIssuanceService
{
    private readonly ITicketRepository _repository;

    public TicketIssuanceService(ITicketRepository repository)
    {
        _repository = repository;
    }

    public async Task<IResult> IssueTicketsAsync(IssueTicketsRequest request)
    {
        if (request.SeatIds == null || request.SeatIds.Count == 0)
        {
            return Results.BadRequest(new { error = "SeatIds cannot be empty." });
        }

        var issuedTickets = new List<TicketDto>();

        foreach (var seatId in request.SeatIds)
        {
            var ticketId = Guid.NewGuid();
            var rawQrData = $"CINEMA-TICKET:{ticketId}:{request.ReservationId}:{seatId}";
            var qrHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawQrData)));

            var ticket = await _repository.CreateTicketAsync(
                ticketId, 
                request.ReservationId, 
                request.ShowtimeId, 
                seatId, 
                qrHash);

            issuedTickets.Add(ticket);
        }

        return Results.Created("/api/v1/tickets", issuedTickets);
    }
}
