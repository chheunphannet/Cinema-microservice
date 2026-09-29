namespace Identity.Api.Models;

public sealed record StaffLoginRequest(string Username, string PinOrPassword, string? TerminalCode);
public sealed record SupervisorVerifyRequest(string? SupervisorUsername, string SupervisorPin);

public record CustomerRegisterRequest(string Email, string Password, string FirstName, string LastName, string? Phone);
public record CustomerLoginRequest(string Email, string Password);
public sealed record GoogleLoginRequest(string IdToken);
public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record CreateStaffRequest(
    string Username,
    string DisplayName,
    string PasswordOrPin,
    string Role,
    Guid? BranchId,
    string? Email,
    string? Phone
);

public sealed record UpdateStaffRequest(
    string? DisplayName,
    string? Role,
    Guid? BranchId,
    string? Email,
    string? Phone,
    bool? IsActive,
    bool ClearBranch = false
);

public sealed record ChangeStaffPasswordRequest(string NewPasswordOrPin);

public sealed record ScheduleShiftRequest(
    Guid UserId,
    Guid BranchId,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    string? TerminalCode,
    string? Notes
);

public sealed record ClockShiftRequest(string Action, string? TerminalCode);

public sealed record UpdateCustomerStatusRequest(bool IsActive, string Reason);
