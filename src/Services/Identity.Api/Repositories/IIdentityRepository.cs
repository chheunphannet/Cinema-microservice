using Identity.Api.Models;

namespace Identity.Api.Repositories;

public interface IIdentityRepository
{
    Task<dynamic?> GetUserByUsernameAsync(string username);
    Task<IEnumerable<dynamic>> GetSupervisorsAsync(string? supervisorUsername);
    Task<IEnumerable<StaffUserDto>> GetUsersAsync(Guid? branchId);
    Task<IEnumerable<RoleDto>> GetRolesAsync();
    Task<Guid> CreateCustomerAsync(string email, string passwordHash, string firstName, string lastName, string? phone);
    Task<dynamic?> GetCustomerByEmailAsync(string email);
    Task<dynamic?> GetCustomerByGoogleIdAsync(string googleId);
    Task<Guid> CreateGoogleCustomerAsync(string email, string firstName, string lastName, string googleId, string? avatarUrl);
    Task LinkGoogleAccountAsync(Guid customerId, string googleId, string? avatarUrl);

    // Enterprise Staff & Shifts Management
    Task<StaffUserDetailDto?> GetStaffByIdAsync(Guid userId);
    Task<IEnumerable<StaffUserDetailDto>> GetStaffListAsync(Guid? branchId, string? role, bool? isActive, string? searchTerm);
    Task<Guid> CreateStaffAsync(CreateStaffRequest request, string pinHash, Guid actorId);
    Task<bool> UpdateStaffAsync(Guid userId, UpdateStaffRequest request, Guid actorId);
    Task<bool> UpdateStaffPasswordAsync(Guid userId, string newPinHash, Guid actorId);
    Task RecordUserAuditAsync(Guid userId, Guid actorId, string action, object details);
    Task<IEnumerable<UserAuditDto>> GetUserAuditLogsAsync(Guid userId);
    Task<Guid> CreateShiftAsync(ScheduleShiftRequest request, Guid actorId);
    Task<IEnumerable<StaffShiftDto>> GetShiftsAsync(Guid? branchId, Guid? userId, DateTimeOffset? fromDate, DateTimeOffset? toDate);
    Task<bool> UpdateShiftClockAsync(Guid shiftId, string action, string? terminalCode, DateTimeOffset timestamp);
    Task UpdateLastLoginAsync(Guid userId);

    // Customer Moderation
    Task<IEnumerable<CustomerAdminDto>> GetCustomersAsync(string? searchTerm, bool? isActive, int limit, int offset);
    Task<bool> UpdateCustomerStatusAsync(Guid customerId, bool isActive, string reason, Guid actorId);
}

