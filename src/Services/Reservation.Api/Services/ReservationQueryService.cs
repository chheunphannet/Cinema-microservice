using Microsoft.AspNetCore.Http;
using Reservation.Api.Repositories;

namespace Reservation.Api.Services;

public class ReservationQueryService : IReservationQueryService
{
    private readonly IReservationRepository _repository;

    public ReservationQueryService(IReservationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IResult> GetReservationDetailsAsync(Guid reservationId)
    {
        var rows = await _repository.GetReservationDetailsAsync(reservationId);
        
        if (rows.Count == 0)
        {
            return Results.NotFound(new { error = $"Reservation {reservationId} not found." });
        }

        var first = rows[0];
        var seats = rows.Where(r => r.seatid != null).Select(r => (Guid)r.seatid).ToList();
        decimal total = rows.Where(r => r.price != null).Sum(r => (decimal)r.price);

        return Results.Ok(new
        {
            reservationId = (Guid)first.reservationid,
            showtimeId = (Guid)first.showtimeid,
            status = (string)first.status,
            confirmedAt = (DateTimeOffset?)first.confirmedat,
            createdAt = (DateTimeOffset)first.createdat,
            seatIds = seats,
            totalAmount = total
        });
    }
}
