using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Reservation.Api.Models;

namespace Reservation.Api.Services;

public interface IAdminBookingService
{
    Task<IResult> SearchBookingsAsync(AdminBookingSearchFilter filter, ClaimsPrincipal user);
    Task<IResult> GetBookingDetailAsync(Guid reservationId, ClaimsPrincipal user);
    Task<IResult> RefundBookingAsync(Guid reservationId, RefundBookingRequest request, ClaimsPrincipal user);
    Task<IResult> ReissueTicketAsync(Guid reservationId, ReissueTicketRequest request, ClaimsPrincipal user);
    Task<IResult> FlagDisputeAsync(Guid reservationId, FlagDisputeRequest request, ClaimsPrincipal user);
}
