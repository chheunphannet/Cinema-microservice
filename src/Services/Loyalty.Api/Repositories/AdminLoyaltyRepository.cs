using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cinema.Foundation.Data;
using Dapper;
using Loyalty.Api.Models;

namespace Loyalty.Api.Repositories;

public class AdminLoyaltyRepository : IAdminLoyaltyRepository
{
    private readonly IDbConnectionFactory _db;

    public AdminLoyaltyRepository(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<AdjustPointsResponse?> AdjustMemberPointsAsync(
        Guid memberId, int pointsDelta, string reason, Guid? referenceOrderId, Guid actorId)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        // 1. Lock and check member
        const string checkSql = @"
            SELECT m.member_id, m.email, m.total_points_balance
            FROM loyalty.members m
            WHERE m.member_id = @MemberId
            FOR UPDATE";

        var member = await conn.QuerySingleOrDefaultAsync<dynamic>(checkSql, new { MemberId = memberId }, tx);
        if (member == null)
        {
            return null;
        }

        int currentBalance = (int)member.total_points_balance;
        string email = (string)member.email;

        if (pointsDelta < 0 && (currentBalance + pointsDelta) < 0)
        {
            throw new InvalidOperationException(
                $"Cannot deduct {Math.Abs(pointsDelta)} points. Current balance is {currentBalance}.");
        }

        // 2. Insert ledger record (triggers trg_update_points_balance to update total_points_balance)
        const string insertLedgerSql = @"
            INSERT INTO loyalty.points_ledger (
                member_id, transaction_type, points_delta, reference_order_id, description, created_at
            ) VALUES (
                @MemberId, 'adjustment'::loyalty.ledger_transaction_type, @PointsDelta, @ReferenceOrderId, @Description, now()
            ) RETURNING ledger_id, created_at";

        var ledgerResult = await conn.QuerySingleAsync<dynamic>(insertLedgerSql, new
        {
            MemberId = memberId,
            PointsDelta = pointsDelta,
            ReferenceOrderId = referenceOrderId,
            Description = reason
        }, tx);

        DateTimeOffset adjustedAt = (DateTimeOffset)ledgerResult.created_at;

        // 3. Read updated balance
        const string balanceSql = "SELECT total_points_balance FROM loyalty.members WHERE member_id = @MemberId";
        int newBalance = await conn.ExecuteScalarAsync<int>(balanceSql, new { MemberId = memberId }, tx);

        // 4. Audit entry
        var details = JsonSerializer.Serialize(new
        {
            pointsDelta,
            previousBalance = currentBalance,
            newBalance,
            reason,
            referenceOrderId
        });

        const string auditSql = @"
            INSERT INTO public.system_audit_log (
                actor_id, service_name, action, resource_type, resource_id, details, occurred_at
            ) VALUES (
                @ActorId, 'Loyalty.Api', 'adjust_points', 'member', @ResourceId, @Details::jsonb, now()
            )";

        await conn.ExecuteAsync(auditSql, new
        {
            ActorId = actorId,
            ResourceId = memberId.ToString(),
            Details = details
        }, tx);

        tx.Commit();

        return new AdjustPointsResponse(
            MemberId: memberId,
            Email: email,
            PointsDelta: pointsDelta,
            NewBalance: newBalance,
            Reason: reason,
            AdjustedAt: adjustedAt
        );
    }

    public async Task<OverrideTierResponse?> OverrideMemberTierAsync(
        Guid memberId, string tierName, string reason, Guid actorId)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        // 1. Lock member and fetch current tier
        const string memberSql = @"
            SELECT m.member_id, m.email, t.name as current_tier_name
            FROM loyalty.members m
            JOIN loyalty.tiers t ON m.tier_id = t.tier_id
            WHERE m.member_id = @MemberId
            FOR UPDATE";

        var member = await conn.QuerySingleOrDefaultAsync<dynamic>(memberSql, new { MemberId = memberId }, tx);
        if (member == null)
        {
            return null;
        }

        string previousTier = (string)member.current_tier_name;
        string email = (string)member.email;

        // 2. Lookup new tier
        const string tierSql = "SELECT tier_id, name FROM loyalty.tiers WHERE LOWER(name) = LOWER(@TierName)";
        var targetTier = await conn.QuerySingleOrDefaultAsync<dynamic>(tierSql, new { TierName = tierName.Trim() }, tx);
        if (targetTier == null)
        {
            throw new ArgumentException($"Invalid tier '{tierName}'. Allowed tiers: Bronze, Silver, Gold, Platinum.");
        }

        Guid newTierId = (Guid)targetTier.tier_id;
        string normalizedNewTier = (string)targetTier.name;

        // 3. Update member tier
        const string updateSql = @"
            UPDATE loyalty.members
            SET tier_id = @TierId, updated_at = now()
            WHERE member_id = @MemberId";

        await conn.ExecuteAsync(updateSql, new { TierId = newTierId, MemberId = memberId }, tx);

        // 4. Audit entry
        var details = JsonSerializer.Serialize(new
        {
            previousTier,
            newTier = normalizedNewTier,
            reason
        });

        const string auditSql = @"
            INSERT INTO public.system_audit_log (
                actor_id, service_name, action, resource_type, resource_id, details, occurred_at
            ) VALUES (
                @ActorId, 'Loyalty.Api', 'override_tier', 'member', @ResourceId, @Details::jsonb, now()
            )";

        await conn.ExecuteAsync(auditSql, new
        {
            ActorId = actorId,
            ResourceId = memberId.ToString(),
            Details = details
        }, tx);

        tx.Commit();

        return new OverrideTierResponse(
            MemberId: memberId,
            Email: email,
            PreviousTier: previousTier,
            NewTier: normalizedNewTier,
            Reason: reason,
            OverriddenAt: DateTimeOffset.UtcNow
        );
    }

    public async Task<AdminMemberListResponse> GetMembersAsync(AdminMemberFilter filter)
    {
        using var conn = _db.CreateReadConnection();

        var sqlBuilder = new StringBuilder(@"
            FROM loyalty.members m
            JOIN loyalty.tiers t ON m.tier_id = t.tier_id
            WHERE 1=1 ");

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            sqlBuilder.Append(@" AND (
                m.email ILIKE @Search OR 
                m.first_name ILIKE @Search OR 
                m.last_name ILIKE @Search OR 
                m.phone ILIKE @Search
            )");
            parameters.Add("Search", $"%{filter.Search.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Tier))
        {
            sqlBuilder.Append(" AND LOWER(t.name) = LOWER(@Tier)");
            parameters.Add("Tier", filter.Tier.Trim());
        }

        if (filter.IsActive.HasValue)
        {
            sqlBuilder.Append(" AND m.is_active = @IsActive");
            parameters.Add("IsActive", filter.IsActive.Value);
        }

        var countSql = "SELECT COUNT(*) " + sqlBuilder;
        int totalCount = await conn.ExecuteScalarAsync<int>(countSql, parameters);

        int page = filter.Page < 1 ? 1 : filter.Page;
        int pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        int offset = (page - 1) * pageSize;

        var querySql = $@"
            SELECT 
                m.member_id, m.first_name, m.last_name, m.email, m.phone,
                t.name as tier_name, t.points_multiplier, m.total_points_balance,
                m.is_active, m.created_at
            {sqlBuilder}
            ORDER BY m.created_at DESC
            LIMIT @Limit OFFSET @Offset";

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", offset);

        var rows = await conn.QueryAsync<dynamic>(querySql, parameters);
        var items = rows.Select(r => new AdminMemberListItem(
            MemberId: (Guid)r.member_id,
            FirstName: (string)r.first_name,
            LastName: (string)r.last_name,
            Email: (string)r.email,
            Phone: (string?)r.phone,
            TierName: (string)r.tier_name,
            PointsMultiplier: (decimal)r.points_multiplier,
            PointsBalance: (int)r.total_points_balance,
            IsActive: (bool)r.is_active,
            CreatedAt: (DateTimeOffset)r.created_at
        )).ToList();

        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new AdminMemberListResponse(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    public async Task<IReadOnlyList<MemberPointsLedgerItem>> GetMemberLedgerAsync(Guid memberId, int limit = 50)
    {
        using var conn = _db.CreateReadConnection();
        const string sql = @"
            SELECT ledger_id, transaction_type::text, points_delta, reference_order_id, description, created_at
            FROM loyalty.points_ledger
            WHERE member_id = @MemberId
            ORDER BY created_at DESC
            LIMIT @Limit";

        var rows = await conn.QueryAsync<dynamic>(sql, new { MemberId = memberId, Limit = Math.Clamp(limit, 1, 100) });
        return rows.Select(r => new MemberPointsLedgerItem(
            LedgerId: (Guid)r.ledger_id,
            TransactionType: (string)r.transaction_type,
            PointsDelta: (int)r.points_delta,
            ReferenceOrderId: (Guid?)r.reference_order_id,
            Description: (string)r.description,
            CreatedAt: (DateTimeOffset)r.created_at
        )).ToList();
    }

    public async Task<CustomerSegmentsResponse> GetCustomerSegmentsAsync()
    {
        using var conn = _db.CreateReadConnection();

        // Single multi-query or aggregation
        const string totalSql = "SELECT COUNT(*) FROM loyalty.members WHERE is_active = true";
        int totalMembers = await conn.ExecuteScalarAsync<int>(totalSql);

        const string vipSql = @"
            SELECT COUNT(*) 
            FROM loyalty.members m
            JOIN loyalty.tiers t ON m.tier_id = t.tier_id
            WHERE m.is_active = true AND (t.name IN ('Gold', 'Platinum') OR m.total_points_balance >= 2000)";
        int vipCount = await conn.ExecuteScalarAsync<int>(vipSql);

        const string activeSql = @"
            SELECT COUNT(DISTINCT member_id)
            FROM loyalty.points_ledger
            WHERE created_at >= now() - interval '30 days'";
        int activeCount = await conn.ExecuteScalarAsync<int>(activeSql);

        const string lapsedSql = @"
            SELECT COUNT(*)
            FROM loyalty.members m
            WHERE m.is_active = true 
              AND m.created_at < now() - interval '60 days'
              AND NOT EXISTS (
                  SELECT 1 FROM loyalty.points_ledger l 
                  WHERE l.member_id = m.member_id AND l.created_at >= now() - interval '60 days'
              )";
        int lapsedCount = await conn.ExecuteScalarAsync<int>(lapsedSql);

        const string newMembersSql = @"
            SELECT COUNT(*) 
            FROM loyalty.members 
            WHERE is_active = true AND created_at >= now() - interval '30 days'";
        int newMembersCount = await conn.ExecuteScalarAsync<int>(newMembersSql);

        decimal CalcPct(int count) => totalMembers > 0 ? Math.Round((decimal)count / totalMembers * 100m, 1) : 0m;

        var segments = new List<CustomerSegmentSummary>
        {
            new("all", "All Active Members", "Total registered active loyalty members", totalMembers, 100m),
            new("vip", "VIP & High Spenders", "Gold/Platinum tier members or >2,000 loyalty points balance", vipCount, CalcPct(vipCount)),
            new("active", "Active (Last 30 Days)", "Members with transactions or points accrual in past 30 days", activeCount, CalcPct(activeCount)),
            new("lapsed", "Lapsed (>60 Days Inactive)", "Members with no points activity in past 60 days", lapsedCount, CalcPct(lapsedCount)),
            new("new_members", "New Signups (<30 Days)", "Members who registered within the last 30 days", newMembersCount, CalcPct(newMembersCount))
        };

        return new CustomerSegmentsResponse(
            Segments: segments,
            TotalMembers: totalMembers,
            GeneratedAt: DateTimeOffset.UtcNow
        );
    }

    public async Task<AdminCampaignDto> CreateCampaignAsync(CreateCampaignRequest request, Guid actorId)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        const string insertSql = @"
            INSERT INTO loyalty.campaigns (
                name, description, target_segment, status, discount_code, discount_value, starts_at, ends_at, created_by, created_at
            ) VALUES (
                @Name, @Description, @TargetSegment, 'active', @DiscountCode, @DiscountValue, @StartsAt, @EndsAt, @CreatedBy, now()
            ) RETURNING campaign_id, created_at";

        var inserted = await conn.QuerySingleAsync<dynamic>(insertSql, new
        {
            request.Name,
            request.Description,
            TargetSegment = string.IsNullOrWhiteSpace(request.TargetSegment) ? "all" : request.TargetSegment.ToLowerInvariant(),
            request.DiscountCode,
            request.DiscountValue,
            request.StartsAt,
            request.EndsAt,
            CreatedBy = actorId
        }, tx);

        Guid campaignId = (Guid)inserted.campaign_id;
        DateTimeOffset createdAt = (DateTimeOffset)inserted.created_at;

        // Audit entry
        var details = JsonSerializer.Serialize(new
        {
            request.Name,
            request.TargetSegment,
            request.DiscountCode,
            request.DiscountValue
        });

        const string auditSql = @"
            INSERT INTO public.system_audit_log (
                actor_id, service_name, action, resource_type, resource_id, details, occurred_at
            ) VALUES (
                @ActorId, 'Loyalty.Api', 'create_campaign', 'campaign', @ResourceId, @Details::jsonb, now()
            )";

        await conn.ExecuteAsync(auditSql, new
        {
            ActorId = actorId,
            ResourceId = campaignId.ToString(),
            Details = details
        }, tx);

        tx.Commit();

        return new AdminCampaignDto(
            CampaignId: campaignId,
            Name: request.Name,
            Description: request.Description,
            TargetSegment: request.TargetSegment,
            Status: "active",
            DiscountCode: request.DiscountCode,
            DiscountValue: request.DiscountValue,
            StartsAt: request.StartsAt,
            EndsAt: request.EndsAt,
            CreatedBy: actorId,
            CreatedAt: createdAt,
            VouchersCount: 0
        );
    }

    public async Task<AdminCampaignsResponse> GetCampaignsAsync(string? status, string? search)
    {
        using var conn = _db.CreateReadConnection();

        var sql = new StringBuilder(@"
            SELECT 
                c.campaign_id, c.name, c.description, c.target_segment, c.status, 
                c.discount_code, c.discount_value, c.starts_at, c.ends_at, c.created_by, c.created_at,
                COUNT(v.voucher_id) as vouchers_count
            FROM loyalty.campaigns c
            LEFT JOIN loyalty.vouchers v ON c.campaign_id = v.campaign_id
            WHERE 1=1 ");

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql.Append(" AND c.status = @Status");
            parameters.Add("Status", status.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql.Append(" AND (c.name ILIKE @Search OR c.description ILIKE @Search OR c.discount_code ILIKE @Search)");
            parameters.Add("Search", $"%{search.Trim()}%");
        }

        sql.Append(@"
            GROUP BY c.campaign_id, c.name, c.description, c.target_segment, c.status, 
                     c.discount_code, c.discount_value, c.starts_at, c.ends_at, c.created_by, c.created_at
            ORDER BY c.created_at DESC");

        var rows = await conn.QueryAsync<dynamic>(sql.ToString(), parameters);
        var items = rows.Select(r => new AdminCampaignDto(
            CampaignId: (Guid)r.campaign_id,
            Name: (string)r.name,
            Description: (string?)r.description,
            TargetSegment: (string)r.target_segment,
            Status: (string)r.status,
            DiscountCode: (string?)r.discount_code,
            DiscountValue: (decimal?)r.discount_value,
            StartsAt: (DateTimeOffset)r.starts_at,
            EndsAt: (DateTimeOffset?)r.ends_at,
            CreatedBy: (Guid)r.created_by,
            CreatedAt: (DateTimeOffset)r.created_at,
            VouchersCount: (int)(long)r.vouchers_count
        )).ToList();

        return new AdminCampaignsResponse(items, items.Count);
    }

    public async Task<IReadOnlyList<string>> GetSegmentRecipientEmailsAsync(string segmentCode)
    {
        using var conn = _db.CreateReadConnection();
        string normalized = segmentCode.Trim().ToLowerInvariant();

        string sql = normalized switch
        {
            "vip" => @"
                SELECT DISTINCT m.email 
                FROM loyalty.members m
                JOIN loyalty.tiers t ON m.tier_id = t.tier_id
                WHERE m.is_active = true AND (t.name IN ('Gold', 'Platinum') OR m.total_points_balance >= 2000)",

            "active" => @"
                SELECT DISTINCT m.email
                FROM loyalty.members m
                JOIN loyalty.points_ledger l ON m.member_id = l.member_id
                WHERE m.is_active = true AND l.created_at >= now() - interval '30 days'",

            "lapsed" => @"
                SELECT DISTINCT m.email
                FROM loyalty.members m
                WHERE m.is_active = true 
                  AND m.created_at < now() - interval '60 days'
                  AND NOT EXISTS (
                      SELECT 1 FROM loyalty.points_ledger l 
                      WHERE l.member_id = m.member_id AND l.created_at >= now() - interval '60 days'
                  )",

            "new_members" => @"
                SELECT DISTINCT email 
                FROM loyalty.members 
                WHERE is_active = true AND created_at >= now() - interval '30 days'",

            _ => "SELECT DISTINCT email FROM loyalty.members WHERE is_active = true"
        };

        var emails = await conn.QueryAsync<string>(sql);
        return emails.ToList();
    }

    public async Task<GenerateVouchersBatchResponse> GenerateVouchersBatchAsync(
        GenerateVouchersBatchRequest request, Guid actorId)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        int count = Math.Clamp(request.Count, 1, 500);
        string prefix = string.IsNullOrWhiteSpace(request.Prefix) ? "PROMO" : request.Prefix.Trim().ToUpperInvariant();
        string voucherType = request.VoucherType.Trim().ToLowerInvariant();
        string targetItemType = request.TargetItemType.Trim().ToLowerInvariant();

        // Validate voucher type enum
        string[] validTypes = { "free_ticket", "percentage_discount", "fixed_discount", "bogo" };
        if (!validTypes.Contains(voucherType))
        {
            throw new ArgumentException($"Invalid voucher type '{voucherType}'. Allowed: {string.Join(", ", validTypes)}");
        }

        var codes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            // Generate distinct 6-character random alphanumeric suffix
            string suffix = RandomNumberGenerator.GetHexString(3).ToUpperInvariant();
            string code = $"{prefix}-{suffix}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            codes.Add(code);
        }

        const string insertSql = @"
            INSERT INTO loyalty.vouchers (
                code, voucher_type, target_item_type, discount_value, expires_at, campaign_id, max_uses, used_count, is_redeemed, created_at
            ) VALUES (
                @Code, @VoucherType::loyalty.voucher_type, @TargetItemType, @DiscountValue, @ExpiresAt, @CampaignId, @MaxUses, 0, false, now()
            )";

        var batchParams = codes.Select(c => new
        {
            Code = c,
            VoucherType = voucherType,
            TargetItemType = targetItemType,
            request.DiscountValue,
            request.ExpiresAt,
            request.CampaignId,
            MaxUses = Math.Max(1, request.MaxUsesPerCode)
        });

        await conn.ExecuteAsync(insertSql, batchParams, tx);

        // Audit entry
        var details = JsonSerializer.Serialize(new
        {
            request.CampaignId,
            prefix,
            count,
            voucherType,
            targetItemType,
            request.DiscountValue,
            codesGenerated = codes.Count
        });

        const string auditSql = @"
            INSERT INTO public.system_audit_log (
                actor_id, service_name, action, resource_type, resource_id, details, occurred_at
            ) VALUES (
                @ActorId, 'Loyalty.Api', 'generate_voucher_batch', 'vouchers', @ResourceId, @Details::jsonb, now()
            )";

        await conn.ExecuteAsync(auditSql, new
        {
            ActorId = actorId,
            ResourceId = request.CampaignId?.ToString() ?? prefix,
            Details = details
        }, tx);

        tx.Commit();

        return new GenerateVouchersBatchResponse(
            CampaignId: request.CampaignId,
            GeneratedCount: codes.Count,
            VoucherCodes: codes,
            VoucherType: voucherType,
            TargetItemType: targetItemType,
            DiscountValue: request.DiscountValue,
            ExpiresAt: request.ExpiresAt,
            CreatedAt: DateTimeOffset.UtcNow
        );
    }
}
