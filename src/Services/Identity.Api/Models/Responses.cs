namespace Identity.Api.Models;

public sealed record StaffUserDto(Guid UserId, string Username, string DisplayName, string Role, Guid? BranchId, bool IsActive);
public sealed record RoleDto(Guid RoleId, string Name, string Description);

public sealed record LoginResponse(
    string Token, Guid UserId, string Username, string DisplayName,
    Guid? BranchId, string[] Roles, DateTimeOffset IssuedAt);

public sealed record SupervisorVerifyResponse(bool Verified, Guid SupervisorId, string SupervisorUsername, string DisplayName);

public static class IdentityConstants
{
    public const string DefaultRole = "cashier";
}

public record CustomerLoginResponse(string Token, Guid CustomerId, string Email, string FirstName, string LastName, string? RefreshToken = null);

public sealed record GoogleLoginResponse(
    string Token,
    string? RefreshToken,
    Guid CustomerId,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string AuthProvider
);

public sealed record CustomerTokenRefreshResponse(string Token, string RefreshToken);

public sealed class StaffUserDetailDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "staff";
    public Guid? BranchId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public StaffUserDetailDto() { }

    public StaffUserDetailDto(Guid userId, string username, string displayName, string role, Guid? branchId, string? email, string? phone, bool isActive, DateTime? lastLoginAt, DateTime createdAt)
    {
        UserId = userId;
        Username = username;
        DisplayName = displayName;
        Role = role;
        BranchId = branchId;
        Email = email;
        Phone = phone;
        IsActive = isActive;
        LastLoginAt = lastLoginAt;
        CreatedAt = createdAt;
    }

    public StaffUserDetailDto(Guid userId, string username, string displayName, string role, Guid? branchId, string? email, string? phone, bool isActive, DateTimeOffset? lastLoginAt, DateTimeOffset createdAt)
        : this(userId, username, displayName, role, branchId, email, phone, isActive, lastLoginAt?.UtcDateTime, createdAt.UtcDateTime)
    {
    }
}

public sealed class StaffShiftDto
{
    public Guid ShiftId { get; set; }
    public Guid UserId { get; set; }
    public string StaffName { get; set; } = "";
    public Guid BranchId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string? TerminalCode { get; set; }
    public string Status { get; set; } = "scheduled";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public StaffShiftDto() { }

    public StaffShiftDto(Guid shiftId, Guid userId, string staffName, Guid branchId, DateTime scheduledStart, DateTime scheduledEnd, DateTime? actualStart, DateTime? actualEnd, string? terminalCode, string status, string? notes, DateTime createdAt)
    {
        ShiftId = shiftId;
        UserId = userId;
        StaffName = staffName;
        BranchId = branchId;
        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledEnd;
        ActualStart = actualStart;
        ActualEnd = actualEnd;
        TerminalCode = terminalCode;
        Status = status;
        Notes = notes;
        CreatedAt = createdAt;
    }

    public StaffShiftDto(Guid shiftId, Guid userId, string staffName, Guid branchId, DateTimeOffset scheduledStart, DateTimeOffset scheduledEnd, DateTimeOffset? actualStart, DateTimeOffset? actualEnd, string? terminalCode, string status, string? notes, DateTimeOffset createdAt)
        : this(shiftId, userId, staffName, branchId, scheduledStart.UtcDateTime, scheduledEnd.UtcDateTime, actualStart?.UtcDateTime, actualEnd?.UtcDateTime, terminalCode, status, notes, createdAt.UtcDateTime)
    {
    }
}

public sealed class UserAuditDto
{
    public Guid AuditId { get; set; }
    public Guid UserId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string DetailsJson { get; set; } = "{}";
    public DateTime OccurredAt { get; set; }

    public UserAuditDto() { }

    public UserAuditDto(Guid auditId, Guid userId, Guid actorId, string action, string detailsJson, DateTime occurredAt)
    {
        AuditId = auditId;
        UserId = userId;
        ActorId = actorId;
        Action = action;
        DetailsJson = detailsJson;
        OccurredAt = occurredAt;
    }

    public UserAuditDto(Guid auditId, Guid userId, Guid actorId, string action, string detailsJson, DateTimeOffset occurredAt)
        : this(auditId, userId, actorId, action, detailsJson, occurredAt.UtcDateTime)
    {
    }
}

public sealed class CustomerAdminDto
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string AuthProvider { get; set; } = "local";
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public CustomerAdminDto() { }

    public CustomerAdminDto(Guid customerId, string email, string firstName, string lastName, string? phone, string? avatarUrl, string authProvider, bool emailVerified, bool isActive, DateTime createdAt)
    {
        CustomerId = customerId;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        Phone = phone;
        AvatarUrl = avatarUrl;
        AuthProvider = authProvider;
        EmailVerified = emailVerified;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public CustomerAdminDto(Guid customerId, string email, string firstName, string lastName, string? phone, string? avatarUrl, string authProvider, bool emailVerified, bool isActive, DateTimeOffset createdAt)
        : this(customerId, email, firstName, lastName, phone, avatarUrl, authProvider, emailVerified, isActive, createdAt.UtcDateTime)
    {
    }
}
