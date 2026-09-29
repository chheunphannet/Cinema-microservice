using System.Data;
using Dapper;
using Loyalty.Api.Models;

namespace Loyalty.Api.Repositories;
using Loyalty.Api.Models;

public interface ILoyaltyRepository
{
    Task<MemberProfileDto?> GetMemberByEmailAsync(string email, IDbConnection conn, IDbTransaction? tx = null);
    Task<VoucherDto?> GetVoucherByCodeAsync(string code, IDbConnection conn, IDbTransaction? tx = null);
}

public class LoyaltyRepository : ILoyaltyRepository
{
    public async Task<MemberProfileDto?> GetMemberByEmailAsync(string email, IDbConnection conn, IDbTransaction? tx = null)
    {
        const string sql = @"
            SELECT 
                m.member_id, m.first_name, m.last_name, m.email, m.phone, 
                t.name as tier_name, t.points_multiplier, m.total_points_balance
            FROM loyalty.members m
            JOIN loyalty.tiers t ON m.tier_id = t.tier_id
            WHERE m.email = @Email AND m.is_active = true";

        var result = await conn.QuerySingleOrDefaultAsync<dynamic>(sql, new { Email = email }, tx);
        if (result == null) return null;

        return new MemberProfileDto(
            (Guid)result.member_id,
            (string)result.first_name,
            (string)result.last_name,
            (string)result.email,
            (string?)result.phone,
            (string)result.tier_name,
            (decimal)result.points_multiplier,
            (int)result.total_points_balance
        );
    }

    public async Task<VoucherDto?> GetVoucherByCodeAsync(string code, IDbConnection conn, IDbTransaction? tx = null)
    {
        const string sql = @"
            SELECT 
                voucher_id, code, voucher_type, target_item_type, 
                discount_value, is_redeemed, expires_at
            FROM loyalty.vouchers
            WHERE code = @Code";

        var result = await conn.QuerySingleOrDefaultAsync<dynamic>(sql, new { Code = code }, tx);
        if (result == null) return null;

        return new VoucherDto(
            (Guid)result.voucher_id,
            (string)result.code,
            (string)result.voucher_type,
            (string)result.target_item_type,
            (decimal)result.discount_value,
            (bool)result.is_redeemed,
            result.expires_at == null ? null : (DateTimeOffset)result.expires_at
        );
    }
}
