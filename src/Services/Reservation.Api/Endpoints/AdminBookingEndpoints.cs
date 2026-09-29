using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reservation.Api.Models;
using Reservation.Api.Services;

namespace Reservation.Api.Endpoints;

public static class AdminBookingEndpoints
{
    public static void MapAdminBookingEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin/bookings")
            .WithTags("Back-Office Admin — Booking & Transaction Operations")
            .RequireAuthorization("BookingsRead");

        // 1. Master Booking Search (supports both /search and root /)
        var searchHandler = async (
            [FromQuery] string? query,
            [FromQuery] Guid? branchId,
            [FromQuery] string? status,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IAdminBookingService bookingService,
            HttpContext context) =>
        {
            int p = page.GetValueOrDefault(1);
            int ps = pageSize.GetValueOrDefault(20);
            var filter = new AdminBookingSearchFilter(
                Query: query,
                BranchId: branchId,
                Status: status,
                FromDate: fromDate,
                ToDate: toDate,
                Page: p > 0 ? p : 1,
                PageSize: ps > 0 ? ps : 20
            );

            return await bookingService.SearchBookingsAsync(filter, context.User);
        };

        admin.MapGet("/search", searchHandler)
            .WithSummary("Master Booking Search")
            .WithDescription("Searches bookings across branches by reference, customer email, phone, or ID with tenant isolation for branch managers.");

        admin.MapGet("", searchHandler)
            .WithSummary("Master Booking Search (Root)")
            .WithDescription("Searches bookings across branches by reference, customer email, phone, or ID with tenant isolation for branch managers.");

        // 2. Booking Detail
        admin.MapGet("/{reservationId:guid}", async (
            Guid reservationId,
            IAdminBookingService bookingService,
            HttpContext context) =>
        {
            return await bookingService.GetBookingDetailAsync(reservationId, context.User);
        })
        .WithSummary("Get Full Booking Detail")
        .WithDescription("Retrieves complete booking details including reservation, movie, auditorium, seats, tickets, concessions, payments, refunds, and disputes.");

        // 3. Atomic Partial / Full Refund
        admin.MapPost("/{reservationId:guid}/refund", async (
            Guid reservationId,
            [FromBody] RefundBookingRequest request,
            IAdminBookingService bookingService,
            HttpContext context) =>
        {
            var effectiveReq = request with { ReservationId = reservationId };
            return await bookingService.RefundBookingAsync(reservationId, effectiveReq, context.User);
        })
        .WithSummary("Atomic Partial or Full Refund").RequireAuthorization("BookingsRefund")
        .WithDescription("Executes an atomic refund releasing confirmed seats, voiding tickets, logging refund audit in pos.refunds, and invalidating Redis seat cache.");

        // 4. Ticket Re-Issuance
        admin.MapPost("/{reservationId:guid}/reissue-ticket", async (
            Guid reservationId,
            [FromBody] ReissueTicketRequest request,
            IAdminBookingService bookingService,
            HttpContext context) =>
        {
            var effectiveReq = request with { ReservationId = reservationId };
            return await bookingService.ReissueTicketAsync(reservationId, effectiveReq, context.User);
        })
        .WithSummary("Re-Issue Ticket Pass & Email")
        .WithDescription("Regenerates ticket passes with optional QR re-hashing and dispatches digital tickets via email with ICS calendar invite.");

        // 5. Dispute Flagging
        admin.MapPost("/{reservationId:guid}/flag-dispute", async (
            Guid reservationId,
            [FromBody] FlagDisputeRequest request,
            IAdminBookingService bookingService,
            HttpContext context) =>
        {
            var effectiveReq = request with { ReservationId = reservationId };
            return await bookingService.FlagDisputeAsync(reservationId, effectiveReq, context.User);
        })
        .WithSummary("Flag Transaction Dispute / Chargeback").RequireAuthorization("BookingsRefund")
        .WithDescription("Records a chargeback or payment dispute record in pos.disputes with evidence notes and tracking status.");
    }
}

