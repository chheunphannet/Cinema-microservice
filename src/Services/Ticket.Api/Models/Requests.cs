namespace Ticket.Api.Models;

public sealed record IssueTicketsRequest(Guid ReservationId, Guid ShowtimeId, List<Guid> SeatIds);
public sealed record PrintTicketRequest(bool IsReprint, string? SupervisorPin);
public sealed record RedeemTicketRequest(string QrToken, string? TerminalCode, Guid? GateId);
