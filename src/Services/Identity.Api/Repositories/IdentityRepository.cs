using Dapper;
using Identity.Api.Models;

namespace Identity.Api.Repositories;

public class IdentityRepository : IIdentityRepository
{
    private readonly Cinema.Foundation.Data.IDbConnectionFactory _dbFactory;

    public IdentityRepository(Cinema.Foundation.Data.IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<dynamic?> GetUserByUsernameAsync(string username)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = @"
            SELECT u.user_id, u.username, u.display_name, u.pin_hash, u.branch_id, u.is_active, r.name as role 
            FROM identity.users u 
            LEFT JOIN identity.user_roles ur ON u.user_id = ur.user_id 
            LEFT JOIN identity.roles r ON ur.role_id = r.role_id 
            WHERE LOWER(u.username) = @Username 
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { Username = username.ToLowerInvariant() });
    }

    public async Task<IEnumerable<dynamic>> GetSupervisorsAsync(string? supervisorUsername)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = @"
            SELECT u.user_id, u.username, u.display_name, u.pin_hash, u.branch_id, u.is_active, r.name as role 
            FROM identity.users u 
            JOIN identity.user_roles ur ON u.user_id = ur.user_id 
            JOIN identity.roles r ON ur.role_id = r.role_id 
            WHERE r.name IN ('supervisor', 'branch_manager', 'system_admin')";
        
        if (!string.IsNullOrWhiteSpace(supervisorUsername))
        {
            sql += " AND LOWER(u.username) = @Username";
        }
        
        return await conn.QueryAsync<dynamic>(sql, new { Username = supervisorUsername?.ToLowerInvariant() });
    }

    public async Task<IEnumerable<StaffUserDto>> GetUsersAsync(Guid? branchId)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = @"
            SELECT u.user_id as UserId, u.username as Username, u.display_name as DisplayName, 
                   COALESCE(r.name, 'cashier') as Role, u.branch_id as BranchId, u.is_active as IsActive 
            FROM identity.users u 
            LEFT JOIN identity.user_roles ur ON u.user_id = ur.user_id 
            LEFT JOIN identity.roles r ON ur.role_id = r.role_id";
        if (branchId.HasValue)
        {
            sql += " WHERE (u.branch_id = @BranchId OR u.branch_id IS NULL OR r.name IN ('super_admin', 'system_admin', 'admin'))";
            return await conn.QueryAsync<StaffUserDto>(sql, new { BranchId = branchId.Value });
        }
        return await conn.QueryAsync<StaffUserDto>(sql);
    }

    public async Task<IEnumerable<RoleDto>> GetRolesAsync()
    {
        using var conn = _dbFactory.CreateConnection();
        return await conn.QueryAsync<RoleDto>("SELECT role_id as RoleId, name as Name, name as Description FROM identity.roles ORDER BY name");
    }

    public async Task<Guid> CreateCustomerAsync(string email, string passwordHash, string firstName, string lastName, string? phone)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = @"
            INSERT INTO identity.customers (email, password_hash, first_name, last_name, phone)
            VALUES (@Email, @Hash, @FirstName, @LastName, @Phone)
            RETURNING customer_id;
        ";
        return await conn.ExecuteScalarAsync<Guid>(sql, new { 
            Email = email.ToLowerInvariant(), 
            Hash = passwordHash, 
            FirstName = firstName, 
            LastName = lastName, 
            Phone = phone 
        });
    }

    public async Task<dynamic?> GetCustomerByEmailAsync(string email)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = "SELECT * FROM identity.customers WHERE LOWER(email) = @Email LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { Email = email.ToLowerInvariant() });
    }

    public async Task<dynamic?> GetCustomerByGoogleIdAsync(string googleId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = "SELECT * FROM identity.customers WHERE google_id = @GoogleId LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { GoogleId = googleId });
    }

    public async Task<Guid> CreateGoogleCustomerAsync(string email, string firstName, string lastName, string googleId, string? avatarUrl)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO identity.customers (email, first_name, last_name, google_id, avatar_url, auth_provider, email_verified)
            VALUES (@Email, @FirstName, @LastName, @GoogleId, @AvatarUrl, 'google', true)
            RETURNING customer_id;
        ";
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            Email = email.ToLowerInvariant(),
            FirstName = firstName,
            LastName = lastName,
            GoogleId = googleId,
            AvatarUrl = avatarUrl
        });
    }

    public async Task LinkGoogleAccountAsync(Guid customerId, string googleId, string? avatarUrl)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE identity.customers
            SET google_id = @GoogleId,
                avatar_url = COALESCE(NULLIF(avatar_url, ''), @AvatarUrl),
                auth_provider = 'google',
                email_verified = true
            WHERE customer_id = @CustomerId;
        ";
        await conn.ExecuteAsync(sql, new
        {
            CustomerId = customerId,
            GoogleId = googleId,
            AvatarUrl = avatarUrl
        });
    }

    public async Task<StaffUserDetailDto?> GetStaffByIdAsync(Guid userId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT u.user_id as UserId, u.username as Username, u.display_name as DisplayName,
                   COALESCE(r.name, 'cashier') as Role, u.branch_id as BranchId,
                   u.email as Email, u.phone as Phone, u.is_active as IsActive,
                   u.last_login_at as LastLoginAt, u.created_at as CreatedAt
            FROM identity.users u
            LEFT JOIN identity.user_roles ur ON u.user_id = ur.user_id
            LEFT JOIN identity.roles r ON ur.role_id = r.role_id
            WHERE u.user_id = @UserId
            LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<StaffUserDetailDto>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<StaffUserDetailDto>> GetStaffListAsync(Guid? branchId, string? role, bool? isActive, string? searchTerm)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = @"
            SELECT u.user_id as UserId, u.username as Username, u.display_name as DisplayName,
                   COALESCE(r.name, 'cashier') as Role, u.branch_id as BranchId,
                   u.email as Email, u.phone as Phone, u.is_active as IsActive,
                   u.last_login_at as LastLoginAt, u.created_at as CreatedAt
            FROM identity.users u
            LEFT JOIN identity.user_roles ur ON u.user_id = ur.user_id
            LEFT JOIN identity.roles r ON ur.role_id = r.role_id
            WHERE 1=1";
        var p = new DynamicParameters();
        if (branchId.HasValue)
        {
            sql += " AND u.branch_id = @BranchId";
            p.Add("BranchId", branchId.Value);
        }
        if (!string.IsNullOrWhiteSpace(role))
        {
            sql += " AND LOWER(r.name) = LOWER(@Role)";
            p.Add("Role", role);
        }
        if (isActive.HasValue)
        {
            sql += " AND u.is_active = @IsActive";
            p.Add("IsActive", isActive.Value);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += " AND (LOWER(u.username) LIKE @Term OR LOWER(u.display_name) LIKE @Term OR LOWER(COALESCE(u.email, '')) LIKE @Term)";
            p.Add("Term", $"%{searchTerm.Trim().ToLowerInvariant()}%");
        }
        sql += " ORDER BY u.created_at DESC;";
        return await conn.QueryAsync<StaffUserDetailDto>(sql, p);
    }

    public async Task<Guid> CreateStaffAsync(CreateStaffRequest request, string pinHash, Guid actorId)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            const string insertUserSql = @"
                INSERT INTO identity.users (username, display_name, pin_hash, branch_id, email, phone, is_active)
                VALUES (@Username, @DisplayName, @PinHash, @BranchId, @Email, @Phone, true)
                RETURNING user_id;";
            var userId = await conn.ExecuteScalarAsync<Guid>(insertUserSql, new
            {
                Username = request.Username.Trim().ToLowerInvariant(),
                DisplayName = request.DisplayName.Trim(),
                PinHash = pinHash,
                BranchId = request.BranchId,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim()
            }, tx);

            const string roleSql = "SELECT role_id FROM identity.roles WHERE LOWER(name) = LOWER(@Role) LIMIT 1;";
            var roleId = await conn.ExecuteScalarAsync<Guid?>(roleSql, new { Role = request.Role }, tx);
            if (!roleId.HasValue)
            {
                roleId = await conn.ExecuteScalarAsync<Guid?>("SELECT role_id FROM identity.roles WHERE name = 'staff' LIMIT 1;", transaction: tx);
            }

            if (roleId.HasValue)
            {
                const string assignRoleSql = "INSERT INTO identity.user_roles (user_id, role_id) VALUES (@UserId, @RoleId) ON CONFLICT DO NOTHING;";
                await conn.ExecuteAsync(assignRoleSql, new { UserId = userId, RoleId = roleId.Value }, tx);
            }

            const string auditSql = @"
                INSERT INTO identity.user_audit (user_id, actor_id, action, details)
                VALUES (@UserId, @ActorId, 'staff_created', @Details::jsonb);";
            var detailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                username = request.Username,
                role = request.Role,
                branchId = request.BranchId,
                email = request.Email
            });
            await conn.ExecuteAsync(auditSql, new { UserId = userId, ActorId = actorId, Details = detailsJson }, tx);

            tx.Commit();
            return userId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateStaffAsync(Guid userId, UpdateStaffRequest request, Guid actorId)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var updates = new List<string>();
            var p = new DynamicParameters();
            p.Add("UserId", userId);

            if (request.DisplayName != null)
            {
                updates.Add("display_name = @DisplayName");
                p.Add("DisplayName", request.DisplayName.Trim());
            }
            if (request.Email != null)
            {
                updates.Add("email = @Email");
                p.Add("Email", string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant());
            }
            if (request.Phone != null)
            {
                updates.Add("phone = @Phone");
                p.Add("Phone", string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim());
            }
            if (request.ClearBranch)
            {
                updates.Add("branch_id = NULL");
            }
            else if (request.BranchId.HasValue)
            {
                updates.Add("branch_id = @BranchId");
                p.Add("BranchId", request.BranchId.Value);
            }
            if (request.IsActive.HasValue)
            {
                updates.Add("is_active = @IsActive");
                p.Add("IsActive", request.IsActive.Value);
            }

            if (updates.Count > 0)
            {
                var sql = $"UPDATE identity.users SET {string.Join(", ", updates)} WHERE user_id = @UserId;";
                await conn.ExecuteAsync(sql, p, tx);
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var roleId = await conn.ExecuteScalarAsync<Guid?>("SELECT role_id FROM identity.roles WHERE LOWER(name) = LOWER(@Role) LIMIT 1;", new { Role = request.Role }, tx);
                if (roleId.HasValue)
                {
                    await conn.ExecuteAsync("DELETE FROM identity.user_roles WHERE user_id = @UserId;", new { UserId = userId }, tx);
                    await conn.ExecuteAsync("INSERT INTO identity.user_roles (user_id, role_id) VALUES (@UserId, @RoleId);", new { UserId = userId, RoleId = roleId.Value }, tx);
                }
            }

            var detailsJson = System.Text.Json.JsonSerializer.Serialize(request);
            await conn.ExecuteAsync(@"
                INSERT INTO identity.user_audit (user_id, actor_id, action, details)
                VALUES (@UserId, @ActorId, 'staff_updated', @Details::jsonb);",
                new { UserId = userId, ActorId = actorId, Details = detailsJson }, tx);

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateStaffPasswordAsync(Guid userId, string newPinHash, Guid actorId)
    {
        using var conn = _dbFactory.CreateConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var rows = await conn.ExecuteAsync("UPDATE identity.users SET pin_hash = @PinHash WHERE user_id = @UserId;", new { UserId = userId, PinHash = newPinHash }, tx);
            if (rows > 0)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO identity.user_audit (user_id, actor_id, action, details)
                    VALUES (@UserId, @ActorId, 'password_reset', '{}'::jsonb);",
                    new { UserId = userId, ActorId = actorId }, tx);
            }
            tx.Commit();
            return rows > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task RecordUserAuditAsync(Guid userId, Guid actorId, string action, object details)
    {
        using var conn = _dbFactory.CreateConnection();
        var detailsJson = System.Text.Json.JsonSerializer.Serialize(details);
        await conn.ExecuteAsync(@"
            INSERT INTO identity.user_audit (user_id, actor_id, action, details)
            VALUES (@UserId, @ActorId, @Action, @Details::jsonb);",
            new { UserId = userId, ActorId = actorId, Action = action, Details = detailsJson });
    }

    public async Task<IEnumerable<UserAuditDto>> GetUserAuditLogsAsync(Guid userId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            SELECT audit_id as AuditId, user_id as UserId, actor_id as ActorId,
                   action as Action, details::text as DetailsJson, occurred_at as OccurredAt
            FROM identity.user_audit
            WHERE user_id = @UserId
            ORDER BY occurred_at DESC;";
        return await conn.QueryAsync<UserAuditDto>(sql, new { UserId = userId });
    }

    public async Task<Guid> CreateShiftAsync(ScheduleShiftRequest request, Guid actorId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO identity.staff_shifts (user_id, branch_id, scheduled_start, scheduled_end, terminal_code, notes)
            VALUES (@UserId, @BranchId, @ScheduledStart, @ScheduledEnd, @TerminalCode, @Notes)
            RETURNING shift_id;";
        var shiftId = await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            UserId = request.UserId,
            BranchId = request.BranchId,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            TerminalCode = request.TerminalCode,
            Notes = request.Notes
        });

        await RecordUserAuditAsync(request.UserId, actorId, "shift_scheduled", new { shiftId, request.BranchId, request.ScheduledStart, request.ScheduledEnd });
        return shiftId;
    }

    public async Task<IEnumerable<StaffShiftDto>> GetShiftsAsync(Guid? branchId, Guid? userId, DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = @"
            SELECT s.shift_id as ShiftId, s.user_id as UserId, u.display_name as StaffName,
                   s.branch_id as BranchId, s.scheduled_start as ScheduledStart,
                   s.scheduled_end as ScheduledEnd, s.actual_start as ActualStart,
                   s.actual_end as ActualEnd, s.terminal_code as TerminalCode,
                   s.status as Status, s.notes as Notes, s.created_at as CreatedAt
            FROM identity.staff_shifts s
            JOIN identity.users u ON s.user_id = u.user_id
            WHERE 1=1";
        var p = new DynamicParameters();
        if (branchId.HasValue)
        {
            sql += " AND s.branch_id = @BranchId";
            p.Add("BranchId", branchId.Value);
        }
        if (userId.HasValue)
        {
            sql += " AND s.user_id = @UserId";
            p.Add("UserId", userId.Value);
        }
        if (fromDate.HasValue)
        {
            sql += " AND s.scheduled_start >= @FromDate";
            p.Add("FromDate", fromDate.Value);
        }
        if (toDate.HasValue)
        {
            sql += " AND s.scheduled_end <= @ToDate";
            p.Add("ToDate", toDate.Value);
        }
        sql += " ORDER BY s.scheduled_start ASC;";
        return await conn.QueryAsync<StaffShiftDto>(sql, p);
    }

    public async Task<bool> UpdateShiftClockAsync(Guid shiftId, string action, string? terminalCode, DateTimeOffset timestamp)
    {
        using var conn = _dbFactory.CreateConnection();
        string sql;
        if (action.Equals("clock_in", StringComparison.OrdinalIgnoreCase))
        {
            sql = @"
                UPDATE identity.staff_shifts
                SET actual_start = @Timestamp,
                    terminal_code = COALESCE(@TerminalCode, terminal_code),
                    status = 'clocked_in'
                WHERE shift_id = @ShiftId;";
        }
        else if (action.Equals("clock_out", StringComparison.OrdinalIgnoreCase))
        {
            sql = @"
                UPDATE identity.staff_shifts
                SET actual_end = @Timestamp,
                    status = 'completed'
                WHERE shift_id = @ShiftId;";
        }
        else
        {
            return false;
        }

        var rows = await conn.ExecuteAsync(sql, new { ShiftId = shiftId, TerminalCode = terminalCode, Timestamp = timestamp });
        return rows > 0;
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        using var conn = _dbFactory.CreateConnection();
        await conn.ExecuteAsync("UPDATE identity.users SET last_login_at = now() WHERE user_id = @UserId;", new { UserId = userId });
    }

    public async Task<IEnumerable<CustomerAdminDto>> GetCustomersAsync(string? searchTerm, bool? isActive, int limit, int offset)
    {
        using var conn = _dbFactory.CreateConnection();
        var sql = @"
            SELECT customer_id as CustomerId, email as Email, first_name as FirstName,
                   last_name as LastName, phone as Phone, avatar_url as AvatarUrl,
                   auth_provider as AuthProvider, email_verified as EmailVerified,
                   is_active as IsActive, created_at as CreatedAt
            FROM identity.customers
            WHERE 1=1";
        var p = new DynamicParameters();
        if (isActive.HasValue)
        {
            sql += " AND is_active = @IsActive";
            p.Add("IsActive", isActive.Value);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += " AND (LOWER(email) LIKE @Term OR LOWER(first_name) LIKE @Term OR LOWER(last_name) LIKE @Term OR LOWER(COALESCE(phone, '')) LIKE @Term)";
            p.Add("Term", $"%{searchTerm.Trim().ToLowerInvariant()}%");
        }
        sql += " ORDER BY created_at DESC LIMIT @Limit OFFSET @Offset;";
        p.Add("Limit", Math.Clamp(limit, 1, 100));
        p.Add("Offset", Math.Max(0, offset));
        return await conn.QueryAsync<CustomerAdminDto>(sql, p);
    }

    public async Task<bool> UpdateCustomerStatusAsync(Guid customerId, bool isActive, string reason, Guid actorId)
    {
        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.ExecuteAsync("UPDATE identity.customers SET is_active = @IsActive WHERE customer_id = @CustomerId;", new { CustomerId = customerId, IsActive = isActive });
        if (rows > 0)
        {
            await RecordUserAuditAsync(customerId, actorId, isActive ? "customer_activated" : "customer_suspended", new { reason });
        }
        return rows > 0;
    }
}
