using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Cinema.Foundation.Email;
using Loyalty.Api.Models;
using Loyalty.Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Loyalty.Api.Services;

public class AdminLoyaltyService : IAdminLoyaltyService
{
    private readonly IAdminLoyaltyRepository _repo;
    private readonly IEmailService _emailService;
    private readonly ILogger<AdminLoyaltyService> _logger;

    public AdminLoyaltyService(
        IAdminLoyaltyRepository repo,
        IEmailService emailService,
        ILogger<AdminLoyaltyService> logger)
    {
        _repo = repo;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<IResult> AdjustMemberPointsAsync(Guid customerId, AdjustPointsRequest request, ClaimsPrincipal user)
    {
        if (request.PointsDelta == 0)
        {
            return Results.BadRequest(new { message = "PointsDelta cannot be 0." });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { message = "Mandatory explanation reason is required for points adjustments." });
        }

        Guid actorId = GetActorId(user);

        try
        {
            var response = await _repo.AdjustMemberPointsAsync(
                customerId, request.PointsDelta, request.Reason.Trim(), request.ReferenceOrderId, actorId);

            if (response == null)
            {
                return Results.NotFound(new { message = $"Loyalty member {customerId} not found." });
            }

            _logger.LogInformation("Admin {ActorId} adjusted points for member {MemberId} by {Delta}. New balance: {NewBalance}",
                actorId, customerId, request.PointsDelta, response.NewBalance);

            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to adjust points for member {MemberId}", customerId);
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to adjust member points.");
        }
    }

    public async Task<IResult> OverrideMemberTierAsync(Guid customerId, OverrideTierRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.TierName))
        {
            return Results.BadRequest(new { message = "TierName is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { message = "Mandatory justification reason is required for tier overrides." });
        }

        Guid actorId = GetActorId(user);

        try
        {
            var response = await _repo.OverrideMemberTierAsync(
                customerId, request.TierName.Trim(), request.Reason.Trim(), actorId);

            if (response == null)
            {
                return Results.NotFound(new { message = $"Loyalty member {customerId} not found." });
            }

            _logger.LogInformation("Admin {ActorId} overrode tier for member {MemberId} from {Prev} to {New}",
                actorId, customerId, response.PreviousTier, response.NewTier);

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to override tier for member {MemberId}", customerId);
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to override member tier.");
        }
    }

    public async Task<IResult> GetMembersAsync(AdminMemberFilter filter)
    {
        var response = await _repo.GetMembersAsync(filter);
        return Results.Ok(response);
    }

    public async Task<IResult> GetMemberLedgerAsync(Guid customerId, int limit = 50)
    {
        var items = await _repo.GetMemberLedgerAsync(customerId, limit);
        return Results.Ok(items);
    }

    public async Task<IResult> GetCustomerSegmentsAsync()
    {
        var segments = await _repo.GetCustomerSegmentsAsync();
        return Results.Ok(segments);
    }

    public async Task<IResult> CreateCampaignAsync(CreateCampaignRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { message = "Campaign Name is required." });
        }

        if (request.EndsAt.HasValue && request.EndsAt <= request.StartsAt)
        {
            return Results.BadRequest(new { message = "EndsAt must be after StartsAt." });
        }

        Guid actorId = GetActorId(user);
        var campaign = await _repo.CreateCampaignAsync(request, actorId);
        return Results.Created($"/api/v1/admin/marketing/promotions?id={campaign.CampaignId}", campaign);
    }

    public async Task<IResult> GetCampaignsAsync(string? status, string? search)
    {
        var response = await _repo.GetCampaignsAsync(status, search);
        return Results.Ok(response);
    }

    public async Task<IResult> BroadcastCampaignAsync(BroadcastCampaignRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.MessageBody))
        {
            return Results.BadRequest(new { message = "Subject and MessageBody are required." });
        }

        string targetSegment = string.IsNullOrWhiteSpace(request.TargetSegment) ? "all" : request.TargetSegment.Trim().ToLowerInvariant();

        // 1. If preview recipient specified, send test message only
        if (!string.IsNullOrWhiteSpace(request.PreviewRecipientEmail))
        {
            string testEmail = request.PreviewRecipientEmail.Trim();
            var testMessage = new EmailMessage(
                ToEmail: testEmail,
                ToName: "Preview Recipient",
                Subject: $"[PREVIEW] {request.Subject}",
                HtmlBody: $"<p><strong>[TEST PREVIEW BROADCAST]</strong></p>" + request.MessageBody,
                PlainTextBody: "[TEST PREVIEW BROADCAST]\n" + request.MessageBody
            );

            var previewResult = await _emailService.SendAsync(testMessage);
            return Results.Ok(new BroadcastCampaignResponse(
                Success: previewResult.Success,
                TargetSegment: targetSegment,
                TotalRecipients: 1,
                SentCount: previewResult.Success ? 1 : 0,
                FailedCount: previewResult.Success ? 0 : 1,
                DispatchedAt: DateTimeOffset.UtcNow,
                PreviewRecipient: testEmail
            ));
        }

        // 2. Query target recipients
        var recipientEmails = await _repo.GetSegmentRecipientEmailsAsync(targetSegment);
        int total = recipientEmails.Count;
        int sent = 0;
        int failed = 0;

        foreach (var email in recipientEmails)
        {
            try
            {
                var msg = new EmailMessage(
                    ToEmail: email,
                    ToName: "Loyalty Member",
                    Subject: request.Subject,
                    HtmlBody: request.MessageBody,
                    PlainTextBody: request.MessageBody
                );

                var dispatch = await _emailService.SendAsync(msg);
                if (dispatch.Success)
                {
                    sent++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch broadcast to {Email}", email);
                failed++;
            }
        }

        _logger.LogInformation("Broadcast dispatched to segment '{Segment}'. Total: {Total}, Sent: {Sent}, Failed: {Failed}",
            targetSegment, total, sent, failed);

        return Results.Ok(new BroadcastCampaignResponse(
            Success: true,
            TargetSegment: targetSegment,
            TotalRecipients: total,
            SentCount: sent,
            FailedCount: failed,
            DispatchedAt: DateTimeOffset.UtcNow
        ));
    }

    public async Task<IResult> GenerateVouchersBatchAsync(GenerateVouchersBatchRequest request, ClaimsPrincipal user)
    {
        if (request.Count is < 1 or > 500)
        {
            return Results.BadRequest(new { message = "Count must be between 1 and 500." });
        }

        if (string.IsNullOrWhiteSpace(request.Prefix))
        {
            return Results.BadRequest(new { message = "Prefix is required." });
        }

        if (request.DiscountValue <= 0)
        {
            return Results.BadRequest(new { message = "DiscountValue must be greater than 0." });
        }

        Guid actorId = GetActorId(user);

        try
        {
            var response = await _repo.GenerateVouchersBatchAsync(request, actorId);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate vouchers batch");
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Failed to generate vouchers batch.");
        }
    }

    private static Guid GetActorId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(idClaim, out var uid) ? uid : Guid.Empty;
    }
}
