using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ticket.Api.Models;
using Ticket.Api.Services;

namespace Ticket.Api.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this IEndpointRouteBuilder routes)
    {
        var tickets = routes.MapGroup("/api/v1/tickets")
            .WithTags("Ticket Issuance & Gate Redemption")
            .RequireAuthorization();

        tickets.MapGet("/health-contract", () => Results.Ok(new
        {
            schema = "tickets",
            stateMachine = new[] { "active", "used", "void" },
            printInvariant = "is_printed transitions false->true exactly once; reprint requires supervisor authorization via body/header",
            redemptionLatencyTarget = "< 500ms"
        }))
        .WithSummary("Ticket Service Architectural Contract")
        .WithDescription("Specifies ticket state transitions, duplicate reprint locks, and barcode verification guarantees.");

        tickets.MapPost("/issue", async (
            [FromBody] IssueTicketsRequest request,
            ITicketIssuanceService issuanceService) =>
        {
            return await issuanceService.IssueTicketsAsync(request);
        })
        .WithSummary("Issue Digital Tickets")
        .WithDescription("Issues entry tickets with cryptographic QR tokens upon confirmed reservation payment in PostgreSQL.");

        tickets.MapPost("/{ticketId:guid}/print", async (
            Guid ticketId,
            [FromBody] PrintTicketRequest? request,
            [FromHeader(Name = "X-Supervisor-Pin")] string? supervisorPinHeader,
            ITicketPrintService printService) =>
        {
            return await printService.PrintTicketAsync(ticketId, request, supervisorPinHeader);
        })
        .WithSummary("Print Physical ESC/POS Ticket Stub")
        .WithDescription("Sends raw ESC/POS byte sequence to thermal printer. Enforces single first-print invariant; supervisor PIN in body/header required for reprints.");

        tickets.MapPost("/redeem", async (
            [FromBody] RedeemTicketRequest request,
            ITicketRedemptionService redemptionService) =>
        {
            return await redemptionService.RedeemTicketAsync(request);
        })
        .WithSummary("Redeem QR Ticket at Gate / Counter Scanner")
        .WithDescription("Validates scanned QR voucher via USB HID / 2D barcode scanner. Updates state to 'used' and logs redemption audit in PostgreSQL.");

        tickets.MapGet("/{ticketId:guid}", async (
            Guid ticketId,
            ITicketQueryService queryService) =>
        {
            return await queryService.GetTicketDetailsAsync(ticketId);
        })
        .WithSummary("Get Ticket Verification Details")
        .WithDescription("Inspects current status, print timestamp, and gate redemption history of a ticket from PostgreSQL.");

        tickets.MapGet("/{ticketId:guid}/e-ticket", async (
            Guid ticketId,
            ITicketQueryService queryService) =>
        {
            return await queryService.GenerateETicketAsync(ticketId);
        })
        .WithSummary("Phase 2 Module 3.2: E-Ticket Payload Generation")
        .WithDescription("Generates cryptographic JSON E-Ticket payload with HMAC-SHA256 signature as specified in Phase 2 Module 3.2.");

        tickets.MapGet("/e-ticket/{ticketId:guid}", async (
            Guid ticketId,
            [FromQuery] long exp,
            [FromQuery] string? sig,
            ITicketQueryService queryService) =>
        {
            return await queryService.GetSignedETicketPassAsync(ticketId, exp, sig);
        })
        .AllowAnonymous()
        .WithSummary("Phase 3: Cryptographic E-Ticket Mobile Pass Viewer")
        .WithDescription("Renders scannable digital ticket pass with HMAC-SHA256 signature verification. Allows zero-login turnstile admission for guest purchasers.");

        tickets.MapGet("/reservation/{reservationId:guid}", async (
            Guid reservationId,
            [FromQuery] long exp,
            [FromQuery] string? sig,
            ITicketQueryService queryService) =>
        {
            return await queryService.GetSignedReservationPassAsync(reservationId, exp, sig);
        })
        .AllowAnonymous()
        .WithSummary("Phase 3: Cryptographic Reservation Group Pass Viewer")
        .WithDescription("Renders all scannable digital ticket passes in a booking with HMAC-SHA256 signature verification. Allows zero-login group admission at turnstiles.");
    }
}

