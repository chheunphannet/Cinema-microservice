namespace Loyalty.Api.Models;

public sealed record MemberProfileDto(Guid MemberId, string FirstName, string LastName, string Email, string? Phone, string TierName, decimal PointsMultiplier, int TotalPointsBalance);
public sealed record VoucherDto(Guid VoucherId, string Code, string VoucherType, string TargetItemType, decimal DiscountValue, bool IsRedeemed, DateTimeOffset? ExpiresAt);
