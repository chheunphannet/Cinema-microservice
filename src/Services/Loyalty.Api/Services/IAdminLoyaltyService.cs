using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Loyalty.Api.Models;

namespace Loyalty.Api.Services;

public interface IAdminLoyaltyService
{
    Task<IResult> AdjustMemberPointsAsync(Guid customerId, AdjustPointsRequest request, ClaimsPrincipal user);
    Task<IResult> OverrideMemberTierAsync(Guid customerId, OverrideTierRequest request, ClaimsPrincipal user);
    Task<IResult> GetMembersAsync(AdminMemberFilter filter);
    Task<IResult> GetMemberLedgerAsync(Guid customerId, int limit = 50);
    Task<IResult> GetCustomerSegmentsAsync();
    Task<IResult> CreateCampaignAsync(CreateCampaignRequest request, ClaimsPrincipal user);
    Task<IResult> GetCampaignsAsync(string? status, string? search);
    Task<IResult> BroadcastCampaignAsync(BroadcastCampaignRequest request, ClaimsPrincipal user);
    Task<IResult> GenerateVouchersBatchAsync(GenerateVouchersBatchRequest request, ClaimsPrincipal user);
}
