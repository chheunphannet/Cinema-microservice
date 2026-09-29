using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Cinema.Foundation.Security;
using Identity.Api.Models;
using Identity.Api.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin")
            .WithTags("Back-Office Admin & RBAC")
            .RequireAuthorization();

        // -------------------------------------------------------------
        // Roles Catalog
        // -------------------------------------------------------------
        admin.MapGet("/staff/roles", async (IIdentityRepository repository) =>
        {
            var roles = await repository.GetRolesAsync();
            return Results.Ok(roles);
        })
        .RequireAuthorization("Staff")
        .WithSummary("Get System Roles")
        .WithDescription("Retrieves all configurable RBAC roles within the cinema system.");

        // -------------------------------------------------------------
        // Staff Accounts Management
        // -------------------------------------------------------------
        admin.MapGet("/staff", async (
            HttpContext context,
            [FromQuery] Guid? branchId,
            [FromQuery] string? role,
            [FromQuery] bool? isActive,
            [FromQuery] string? search,
            IIdentityRepository repository) =>
        {
            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            if (!isSuperAdmin)
            {
                if (callerBranchId == null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Caller has no branch assignment.");
                }
                if (branchId.HasValue && branchId.Value != callerBranchId.Value)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access staff of a different branch.");
                }
                branchId = callerBranchId;
            }

            var staffList = await repository.GetStaffListAsync(branchId, role, isActive, search);
            return Results.Ok(staffList);
        })
        .RequireAuthorization("BranchManager")
        .WithSummary("List Staff Members")
        .WithDescription("Retrieves staff members filtered by branch, role, active status, or search keywords.");

        admin.MapGet("/staff/{userId:guid}", async (
            HttpContext context,
            Guid userId,
            IIdentityRepository repository) =>
        {
            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            var staff = await repository.GetStaffByIdAsync(userId);
            if (staff == null)
            {
                return Results.NotFound(new { message = $"Staff member {userId} not found." });
            }

            if (!isSuperAdmin && callerBranchId.HasValue && staff.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot access staff of a different branch.");
            }

            return Results.Ok(staff);
        })
        .RequireAuthorization("BranchManager")
        .WithSummary("Get Staff Member Details")
        .WithDescription("Retrieves full details for a specific staff user.");

        admin.MapPost("/staff", async (
            HttpContext context,
            [FromBody] CreateStaffRequest request,
            IIdentityRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.PasswordOrPin))
            {
                return Results.BadRequest(new { message = "Username, DisplayName, and PasswordOrPin are required." });
            }

            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            var callerId = GetCallerUserId(context);

            var targetBranchId = request.BranchId;
            // Enforce logical role and branch scope
            var normalizedRole = request.Role?.Trim().ToLowerInvariant() ?? "staff";
            if (!isSuperAdmin)
            {
                if (callerBranchId == null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Caller has no branch assignment.");
                }
                if (targetBranchId.HasValue && targetBranchId.Value != callerBranchId.Value)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot create staff for another branch.");
                }
                targetBranchId = callerBranchId;

                // Branch managers cannot create super_admin or system_admin accounts
                if (normalizedRole is "super_admin" or "system_admin")
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Branch managers cannot provision super admins.");
                }
            }

            bool isSystemRole = normalizedRole is "super_admin" or "system_admin" or "content_manager";
            if (isSystemRole)
            {
                targetBranchId = null; // System-wide roles operate globally and cannot be assigned to a single branch
            }
            else if (!targetBranchId.HasValue)
            {
                return Results.BadRequest(new { message = $"Branch assignment is required for branch-scoped role '{request.Role}'." });
            }

            var existing = await repository.GetUserByUsernameAsync(request.Username);
            if (existing != null)
            {
                return Results.Conflict(new { message = $"Username '{request.Username}' is already in use." });
            }

            var pinHash = PasswordHasher.Hash(request.PasswordOrPin);
            var sanitizedRequest = request with { BranchId = targetBranchId };
            var newUserId = await repository.CreateStaffAsync(sanitizedRequest, pinHash, callerId);

            return Results.Created($"/api/v1/admin/staff/{newUserId}", new { userId = newUserId, username = sanitizedRequest.Username });
        })
        .RequireAuthorization("BranchManager")
        .WithSummary("Create Staff Member")
        .WithDescription("Provisions a new staff account with assigned role and credentials.");

        admin.MapPut("/staff/{userId:guid}", async (
            HttpContext context,
            Guid userId,
            [FromBody] UpdateStaffRequest request,
            IIdentityRepository repository) =>
        {
            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            var callerId = GetCallerUserId(context);

            var staff = await repository.GetStaffByIdAsync(userId);
            if (staff == null)
            {
                return Results.NotFound(new { message = $"Staff member {userId} not found." });
            }

            var normalizedRole = request.Role?.Trim().ToLowerInvariant();
            if (!isSuperAdmin)
            {
                if (callerBranchId.HasValue && staff.BranchId != callerBranchId.Value)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot modify staff from a different branch.");
                }
                if (request.BranchId.HasValue && request.BranchId.Value != callerBranchId)
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot reassign staff to a different branch.");
                }
                if (normalizedRole is "super_admin" or "system_admin")
                {
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Branch managers cannot elevate roles to super admin.");
                }
            }

            // Enforce logical role and branch scope
            var effectiveRole = normalizedRole ?? staff.Role.ToLowerInvariant();
            bool isSystemRole = effectiveRole is "super_admin" or "system_admin" or "content_manager";
            Guid? sanitizedBranchId = request.BranchId;
            bool clearBranch = request.ClearBranch;

            if (isSystemRole)
            {
                sanitizedBranchId = null;
                clearBranch = true; // Always clear branch assignment for system-wide roles
            }
            else if (request.Role != null || request.BranchId.HasValue || request.ClearBranch)
            {
                var finalBranchId = sanitizedBranchId ?? staff.BranchId;
                if (!finalBranchId.HasValue || (clearBranch && !sanitizedBranchId.HasValue))
                {
                    return Results.BadRequest(new { message = $"Branch assignment is required for branch-scoped role '{effectiveRole}'." });
                }
            }

            var sanitizedRequest = request with { BranchId = sanitizedBranchId, ClearBranch = clearBranch };
            var success = await repository.UpdateStaffAsync(userId, sanitizedRequest, callerId);
            if (!success)
            {
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to update staff member.");
            }

            var updated = await repository.GetStaffByIdAsync(userId);
            return Results.Ok(updated);
        })
        .RequireAuthorization("BranchManager")
        .WithSummary("Update Staff Member")
        .WithDescription("Updates profile, assigned role, branch, or active status of a staff member.");

        admin.MapPost("/staff/{userId:guid}/password", async (
            HttpContext context,
            Guid userId,
            [FromBody] ChangeStaffPasswordRequest request,
            IIdentityRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPasswordOrPin))
            {
                return Results.BadRequest(new { message = "NewPasswordOrPin cannot be empty." });
            }

            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            var callerId = GetCallerUserId(context);

            var staff = await repository.GetStaffByIdAsync(userId);
            if (staff == null)
            {
                return Results.NotFound(new { message = $"Staff member {userId} not found." });
            }

            if (!isSuperAdmin && callerBranchId.HasValue && staff.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot reset password for staff of another branch.");
            }

            var pinHash = PasswordHasher.Hash(request.NewPasswordOrPin);
            var success = await repository.UpdateStaffPasswordAsync(userId, pinHash, callerId);
            if (!success)
            {
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to update credentials.");
            }

            return Results.Ok(new { message = "Password/PIN successfully updated." });
        })
        .RequireAuthorization("BranchManager")
        .WithSummary("Reset Staff Credentials")
        .WithDescription("Resets a staff member's login PIN or password.");

        admin.MapGet("/staff/{userId:guid}/audit", async (
            HttpContext context,
            Guid userId,
            IIdentityRepository repository) =>
        {
            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            var staff = await repository.GetStaffByIdAsync(userId);
            if (staff == null)
            {
                return Results.NotFound(new { message = $"Staff member {userId} not found." });
            }

            if (!isSuperAdmin && callerBranchId.HasValue && staff.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot view audit logs of staff in another branch.");
            }

            var logs = await repository.GetUserAuditLogsAsync(userId);
            return Results.Ok(logs);
        })
        .RequireAuthorization("BranchManager")
        .WithSummary("Get Staff User Audit Trail")
        .WithDescription("Retrieves history of role changes, password resets, and account modifications.");

        // -------------------------------------------------------------
        // Staff Shift Scheduling & Clocking
        // -------------------------------------------------------------
        admin.MapGet("/shifts", async (
            HttpContext context,
            [FromQuery] Guid? branchId,
            [FromQuery] Guid? userId,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            IIdentityRepository repository) =>
        {
            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            var callerId = GetCallerUserId(context);

            if (!isSuperAdmin)
            {
                // If standard staff (not branch manager), restrict to own shifts
                var isManager = context.User.IsInRole("branch_manager");
                if (!isManager)
                {
                    userId = callerId;
                }
                else if (callerBranchId.HasValue)
                {
                    branchId = callerBranchId;
                }
            }

            var shifts = await repository.GetShiftsAsync(branchId, userId, fromDate, toDate);
            return Results.Ok(shifts);
        })
        .RequireAuthorization("Staff")
        .WithSummary("List Shift Schedules")
        .WithDescription("Retrieves scheduled and executed staff shifts with optional branch or user filtering.");

        admin.MapPost("/shifts", async (
            HttpContext context,
            [FromBody] ScheduleShiftRequest request,
            IIdentityRepository repository) =>
        {
            if (request.ScheduledEnd <= request.ScheduledStart)
            {
                return Results.BadRequest(new { message = "ScheduledEnd must be after ScheduledStart." });
            }

            var (callerBranchId, isSuperAdmin) = GetCallerContext(context);
            var callerId = GetCallerUserId(context);

            if (!isSuperAdmin && callerBranchId.HasValue && request.BranchId != callerBranchId.Value)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cannot schedule shifts for another branch.");
            }

            var shiftId = await repository.CreateShiftAsync(request, callerId);
            return Results.Created($"/api/v1/admin/shifts?shiftId={shiftId}", new { shiftId });
        })
        .RequireAuthorization("BranchManager")
        .WithSummary("Schedule Staff Shift")
        .WithDescription("Assigns a shift to a staff member at a designated branch and terminal.");

        admin.MapPost("/shifts/{shiftId:guid}/clock", async (
            HttpContext context,
            Guid shiftId,
            [FromBody] ClockShiftRequest request,
            IIdentityRepository repository) =>
        {
            if (!string.Equals(request.Action, "clock_in", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.Action, "clock_out", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = "Action must be 'clock_in' or 'clock_out'." });
            }

            var success = await repository.UpdateShiftClockAsync(shiftId, request.Action, request.TerminalCode, DateTimeOffset.UtcNow);
            if (!success)
            {
                return Results.NotFound(new { message = $"Shift {shiftId} not found or invalid transition." });
            }

            return Results.Ok(new { message = $"Successfully performed {request.Action} for shift {shiftId}." });
        })
        .RequireAuthorization("Staff")
        .WithSummary("Clock In / Out")
        .WithDescription("Records actual clock-in or clock-out timestamps and terminal identifier for a scheduled shift.");

        // -------------------------------------------------------------
        // Customer Account Moderation
        // -------------------------------------------------------------
        admin.MapGet("/customers", async (
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromQuery] int limit = 20,
            [FromQuery] int offset = 0,
            IIdentityRepository repository = null!) =>
        {
            var customers = await repository.GetCustomersAsync(search, isActive, limit, offset);
            return Results.Ok(customers);
        })
        .RequireAuthorization("CustomerSupport")
        .WithSummary("Search & List Customers")
        .WithDescription("Searches customer accounts by email, name, or phone number with pagination.");

        admin.MapPatch("/customers/{customerId:guid}/status", async (
            HttpContext context,
            Guid customerId,
            [FromBody] UpdateCustomerStatusRequest request,
            IIdentityRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { message = "Reason for customer status change is mandatory." });
            }

            var callerId = GetCallerUserId(context);
            var success = await repository.UpdateCustomerStatusAsync(customerId, request.IsActive, request.Reason, callerId);
            if (!success)
            {
                return Results.NotFound(new { message = $"Customer {customerId} not found." });
            }

            return Results.Ok(new
            {
                customerId,
                isActive = request.IsActive,
                status = request.IsActive ? "Active" : "Suspended",
                reason = request.Reason
            });
        })
        .RequireAuthorization("CustomerSupport")
        .WithSummary("Customer Moderation Status")
        .WithDescription("Suspends or reactivates a customer account with mandatory audit explanation.");
    }

    private static (Guid? BranchId, bool IsSuperAdmin) GetCallerContext(HttpContext context)
    {
        var isSuperAdmin = context.User.IsInRole("super_admin") || context.User.IsInRole("system_admin");
        var branchClaim = context.User.FindFirst("branchId")?.Value 
            ?? context.User.FindFirst("branch_id")?.Value;
        Guid? branchId = Guid.TryParse(branchClaim, out var b) ? b : null;
        return (branchId, isSuperAdmin);
    }

    private static Guid GetCallerUserId(HttpContext context)
    {
        var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(idClaim, out var uid) ? uid : Guid.Empty;
    }
}
