using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loyalty.Api.Models;

namespace Loyalty.Api.Repositories;

public interface IAdminLoyaltyRepository
{
    Task<AdjustPointsResponse?> AdjustMemberPointsAsync(Guid memberId, int pointsDelta, string reason, Guid? referenceOrderId, Guid actorId);
    Task<OverrideTierResponse?> OverrideMemberTierAsync(Guid memberId, string tierName, string reason, Guid actorId);
    Task<AdminMemberListResponse> GetMembersAsync(AdminMemberFilter filter);
    Task<IReadOnlyList<MemberPointsLedgerItem>> GetMemberLedgerAsync(Guid memberId, int limit = 50);
    Task<CustomerSegmentsResponse> GetCustomerSegmentsAsync();
    Task<AdminCampaignDto> CreateCampaignAsync(CreateCampaignRequest request, Guid actorId);
    Task<AdminCampaignsResponse> GetCampaignsAsync(string? status, string? search);
    Task<IReadOnlyList<string>> GetSegmentRecipientEmailsAsync(string segmentCode);
    Task<GenerateVouchersBatchResponse> GenerateVouchersBatchAsync(GenerateVouchersBatchRequest request, Guid actorId);
}
