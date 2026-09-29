using Cinema.Foundation.Security;
using Identity.Api.Models;
using Identity.Api.Repositories;

namespace Identity.Api.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IIdentityRepository _repository;
    private readonly IJwtTokenService _jwtService;

    public AuthenticationService(IIdentityRepository repository, IJwtTokenService jwtService)
    {
        _repository = repository;
        _jwtService = jwtService;
    }

    public async Task<IResult> LoginAsync(StaffLoginRequest request)
    {
        var user = await _repository.GetUserByUsernameAsync(request.Username);
        if (user == null)
        {
            return Results.Unauthorized();
        }

        bool active = user.is_active;
        if (!active)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Staff account is disabled");
        }

        string storedHash = user.pin_hash ?? "";
        if (!PasswordHasher.Verify(request.PinOrPassword, storedHash))
        {
            return Results.Unauthorized();
        }

                Guid userId = user.user_id;
        Guid? branchId = user.branch_id;
        string displayName = user.display_name;
        string role = user.role ?? IdentityConstants.DefaultRole;

        var permissions = GetPermissionsForRole(role);
        var extraClaims = permissions.Select(p => new System.Security.Claims.Claim("permission", p)).ToList();

        var token = _jwtService.GenerateToken(userId, request.Username, displayName, role, branchId, TimeSpan.FromHours(8), extraClaims);
        await _repository.UpdateLastLoginAsync(userId);

        return Results.Ok(new
        {
            token,
            userId,
            username = request.Username,
            displayName,
            branchId,
            roles = new[] { role },
            issuedAt = DateTimeOffset.UtcNow
        });
    }

    private static string[] GetPermissionsForRole(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "super_admin" => new[] { "*.*" },
            "branch_manager" => new[] { "bookings.read", "bookings.refund", "inventory.adjust", "inventory.read", "showtimes.read", "showtimes.write", "staff.read", "reports.read", "screens.read", "screens.write" },
            "content_manager" => new[] { "movies.write", "movies.read", "pricing.write", "pricing.read", "promotions.write", "promotions.read", "cms.write" },
            "finance_manager" => new[] { "reports.read", "finance.manage", "bookings.read", "inventory.audit" },
            "marketing_manager" => new[] { "promotions.write", "promotions.read", "reports.read", "customers.read", "customers.manage" },
            "customer_support" => new[] { "bookings.read", "bookings.refund", "customers.manage", "customers.read" },
            "inventory_manager" => new[] { "inventory.adjust", "inventory.read" },
            "staff" or "cashier" => new[] { "bookings.read", "inventory.read" },
            _ => Array.Empty<string>()
        };
    }
}

