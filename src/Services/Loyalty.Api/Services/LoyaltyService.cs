using Cinema.Foundation.Data;
using Microsoft.AspNetCore.Http;
using Npgsql;



namespace Loyalty.Api.Services;
using Loyalty.Api.Models;
using Loyalty.Api.Repositories;

public interface ILoyaltyService
{
    Task<IResult> GetMemberProfileAsync(string email);
    Task<IResult> ValidateVoucherAsync(ValidateVoucherRequest request);
}

public class LoyaltyService : ILoyaltyService
{
    private readonly ILoyaltyRepository _repository;
    private readonly IDbConnectionFactory _dbFactory;

    public LoyaltyService(ILoyaltyRepository repository, IDbConnectionFactory dbFactory)
    {
        _repository = repository;
        _dbFactory = dbFactory;
    }

    public async Task<IResult> GetMemberProfileAsync(string email)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var member = await _repository.GetMemberByEmailAsync(email, conn);
        
        if (member == null)
            return Results.NotFound(new { error = $"Active loyalty member with email {email} not found." });

        return Results.Ok(member);
    }

    public async Task<IResult> ValidateVoucherAsync(ValidateVoucherRequest request)
    {
        using var conn = _dbFactory.CreateReadConnection();
        var voucher = await _repository.GetVoucherByCodeAsync(request.Code, conn);

        if (voucher == null)
            return Results.NotFound(new { error = "Invalid voucher code." });

        if (voucher.IsRedeemed)
            return Results.BadRequest(new { error = "Voucher has already been redeemed." });

        if (voucher.ExpiresAt.HasValue && voucher.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return Results.BadRequest(new { error = "Voucher is expired." });

        // Optionally, check if it targets the item type requested (if the client provides one to check)
        if (!string.IsNullOrWhiteSpace(request.TargetItemType) && 
            !string.Equals(voucher.TargetItemType, request.TargetItemType, StringComparison.OrdinalIgnoreCase) && 
            !string.Equals(voucher.TargetItemType, "order_total", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = $"This voucher is valid for {voucher.TargetItemType}, not {request.TargetItemType}." });
        }

        return Results.Ok(voucher);
    }
}
