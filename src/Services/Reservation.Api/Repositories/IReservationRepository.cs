using System.Data.Common;

namespace Reservation.Api.Repositories;

public interface IReservationRepository
{
    Task<List<Guid>> GetAlreadyBookedSeatsAsync(Guid showtimeId, IEnumerable<Guid> seatIds);
    Task<List<Guid>> GetBlockedSeatsAsync(Guid showtimeId, IEnumerable<Guid> seatIds);
    Task<List<dynamic>> GetRowSeatsContextAsync(Guid showtimeId, IEnumerable<Guid> requestedSeatIds);
    Task<Dictionary<Guid, decimal>> GetSeatPricesAsync(Guid showtimeId, Dictionary<Guid, Guid> seatTicketTypes);
    Task CreateHoldAsync(Guid holdId, Guid showtimeId, Guid? customerId, DateTimeOffset expiresAt, Guid idempotencyKey, long fencingToken, string? guestEmail = null, string? guestPhone = null, string? guestName = null);
    Task InsertHoldSeatsAsync(Guid reservationId, Guid showtimeId, List<Guid> seatIds, Dictionary<Guid, Guid> seatTicketTypes);
    Task CancelHoldAsync(Guid holdId);
    
    Task<dynamic?> GetReservationByIdempotencyKeyOrHoldIdAsync(Guid idempotencyKey, Guid holdId, DbConnection conn, DbTransaction? tx = null);
    Task<long> GetMaxFencingTokenForSeatsAsync(Guid showtimeId, IEnumerable<Guid> seatIds, DbConnection conn, DbTransaction? tx = null);
    
    Task<int> ConfirmExistingReservationAsync(Guid reservationId, long fencingToken, DbConnection conn, DbTransaction tx);
    Task InsertNewConfirmedReservationAsync(Guid reservationId, Guid showtimeId, Guid idempotencyKey, long fencingToken, DbConnection conn, DbTransaction tx);
    Task InsertReservationSeatAsync(Guid reservationId, Guid seatId, decimal price, DbConnection conn, DbTransaction tx);
    Task InsertConfirmedSeatAsync(Guid showtimeId, Guid seatId, Guid reservationId, DbConnection conn, DbTransaction tx);
    
    Task<List<dynamic>> GetReservationDetailsAsync(Guid reservationId);
    Task<dynamic?> FindActiveReservationByIdempotencyKeyAsync(Guid idempotencyKey);
    Task<List<Guid>> GetReservationSeatsAsync(Guid reservationId);
    Task<int> CleanupExpiredHoldsAsync();
}
