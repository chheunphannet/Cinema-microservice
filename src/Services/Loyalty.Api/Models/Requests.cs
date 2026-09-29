namespace Loyalty.Api.Models;

public sealed record ValidateVoucherRequest(string Code, string TargetItemType);
