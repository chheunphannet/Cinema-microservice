using System;
using System.Collections.Generic;

namespace Loyalty.Api.Models;

public record AdjustPointsRequest(
    int PointsDelta,
    string Reason,
    Guid? ReferenceOrderId = null
);

public record AdjustPointsResponse(
    Guid MemberId,
    string Email,
    int PointsDelta,
    int NewBalance,
    string Reason,
    DateTimeOffset AdjustedAt
);

public record OverrideTierRequest(
    string TierName,
    string Reason
);

public record OverrideTierResponse(
    Guid MemberId,
    string Email,
    string PreviousTier,
    string NewTier,
    string Reason,
    DateTimeOffset OverriddenAt
);

public record AdminMemberFilter(
    string? Search = null,
    string? Tier = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

public record AdminMemberListItem(
    Guid MemberId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string TierName,
    decimal PointsMultiplier,
    int PointsBalance,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record AdminMemberListResponse(
    IReadOnlyList<AdminMemberListItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record MemberPointsLedgerItem(
    Guid LedgerId,
    string TransactionType,
    int PointsDelta,
    Guid? ReferenceOrderId,
    string Description,
    DateTimeOffset CreatedAt
);

public record CustomerSegmentSummary(
    string SegmentCode,
    string Name,
    string Description,
    int MemberCount,
    decimal Percentage
);

public record CustomerSegmentsResponse(
    IReadOnlyList<CustomerSegmentSummary> Segments,
    int TotalMembers,
    DateTimeOffset GeneratedAt
);

public record CreateCampaignRequest(
    string Name,
    string? Description,
    string TargetSegment,
    string? DiscountCode,
    decimal? DiscountValue,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt
);

public record AdminCampaignDto(
    Guid CampaignId,
    string Name,
    string? Description,
    string TargetSegment,
    string Status,
    string? DiscountCode,
    decimal? DiscountValue,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    int VouchersCount
);

public record AdminCampaignsResponse(
    IReadOnlyList<AdminCampaignDto> Items,
    int TotalCount
);

public record BroadcastCampaignRequest(
    Guid? CampaignId,
    string TargetSegment,
    string Subject,
    string MessageBody,
    string? PreviewRecipientEmail = null
);

public record BroadcastCampaignResponse(
    bool Success,
    string TargetSegment,
    int TotalRecipients,
    int SentCount,
    int FailedCount,
    DateTimeOffset DispatchedAt,
    string? PreviewRecipient = null
);

public record GenerateVouchersBatchRequest(
    Guid? CampaignId,
    string Prefix,
    int Count,
    string VoucherType,
    string TargetItemType,
    decimal DiscountValue,
    DateTimeOffset? ExpiresAt,
    int MaxUsesPerCode = 1
);

public record GenerateVouchersBatchResponse(
    Guid? CampaignId,
    int GeneratedCount,
    IReadOnlyList<string> VoucherCodes,
    string VoucherType,
    string TargetItemType,
    decimal DiscountValue,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt
);
