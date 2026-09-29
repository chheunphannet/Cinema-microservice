using Identity.Api.Models;
using Identity.Api.Repositories;
using Identity.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;

namespace Identity.Api.Endpoints;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder routes)
    {
        var identity = routes.MapGroup("/api/v1/identity")
            .WithTags("Identity & Staff Management")
            .RequireAuthorization();

        identity.MapGet("/health-contract", () => Results.Content(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                schema = "identity",
                roles = new[] { "cashier", "supervisor", "branch_manager", "system_admin" },
                terminalAuth = "Touchscreen Cashier PIN & Supervisor Override Supported"
            }), "application/json"))
        .AllowAnonymous()
        .WithSummary("Identity Service Architectural Contract")
        .WithDescription("Defines security roles, cashier terminal session rules, and token claims.");

        identity.MapPost("/login", async (
            [FromBody] StaffLoginRequest request,
            IAuthenticationService authService) =>
        {
            return await authService.LoginAsync(request);
        })
        .AllowAnonymous()
        .WithSummary("Staff / Cashier Terminal Login")
        .WithDescription("Authenticates cashier or supervisor using username and PIN, returning a cryptographically signed HMAC-SHA256 JWT.");

        identity.MapPost("/verify-supervisor", async (
            [FromBody] SupervisorVerifyRequest request,
            ISupervisorService supervisorService) =>
        {
            return await supervisorService.VerifySupervisorPinAsync(request);
        })
        .WithSummary("Verify Supervisor Override PIN")
        .WithDescription("Securely validates a supervisor PIN via POST body rather than query params to prevent audit log leakage.");

        identity.MapGet("/users", async (
            HttpContext context,
            [FromQuery] Guid? branchId,
            IIdentityRepository repository) =>
        {
            var branchIdClaim = context.User.FindFirst("branchId")?.Value;
            if (Guid.TryParse(branchIdClaim, out var parsed))
            {
                branchId = parsed;
            }

            var users = await repository.GetUsersAsync(branchId);
            return Results.Ok(users);
        })
        .AllowAnonymous();

        var customers = routes.MapGroup("/api/v1/identity/customers")
            .WithTags("Customer Portal Auth");

        customers.MapPost("/register", async (
            [FromBody] CustomerRegisterRequest request,
            ICustomerAuthService customerAuthService) =>
        {
            return await customerAuthService.RegisterAsync(request);
        })
        .AllowAnonymous()
        .WithSummary("Customer Registration")
        .WithDescription("Registers a new customer account with email and password.");

        customers.MapPost("/login", async (
            [FromBody] CustomerLoginRequest request,
            ICustomerAuthService customerAuthService) =>
        {
            return await customerAuthService.LoginAsync(request);
        })
        .AllowAnonymous()
        .WithSummary("Customer Login")
        .WithDescription("Authenticates a customer using email and password, returning RS256 JWT access token.");

        customers.MapPost("/google-login", async (
            [FromBody] GoogleLoginRequest request,
            ICustomerAuthService customerAuthService) =>
        {
            return await customerAuthService.GoogleLoginAsync(request);
        })
        .AllowAnonymous()
        .WithSummary("Customer Google Social Login")
        .WithDescription("Authenticates or registers a customer via verified Google ID token (RS256 JWT), returning access and refresh tokens.");

        customers.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            ICustomerAuthService customerAuthService) =>
        {
            return await customerAuthService.RefreshTokenAsync(request);
        })
        .AllowAnonymous()
        .WithSummary("Refresh Customer Access Token")
        .WithDescription("Rotates a customer refresh token and returns a newly minted access token.");
    }
}
