using Ticket.Api.Models;

namespace Ticket.Api.Repositories;

public interface ITicketRepository
{
    Task<TicketDto> CreateTicketAsync(Guid ticketId, Guid reservationId, Guid showtimeId, Guid seatId, string qrHash);
    Task<dynamic?> GetTicketForPrintAsync(Guid ticketId);
    Task<IEnumerable<string>> GetSupervisorPinHashesAsync();
    Task MarkTicketPrintedAsync(Guid ticketId);
    Task LogAuditAsync(Guid ticketId, string action, string terminalCode, string metadataJson);
    Task<dynamic?> GetTicketForRedemptionAsync(string qrHash, string rawToken);
    Task<bool> MarkTicketRedeemedAsync(Guid ticketId);
    Task<dynamic?> GetTicketDetailsAsync(Guid ticketId);
    Task<dynamic?> GetETicketInfoAsync(Guid ticketId);
    Task<List<dynamic>> GetTicketsByReservationIdAsync(Guid reservationId);
    Task VoidTicketsByReservationAsync(Guid reservationId, IEnumerable<Guid>? seatIds = null);
}
