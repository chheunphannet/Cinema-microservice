using Identity.Api.Models;

namespace Identity.Api.Services;

public interface IAuthenticationService
{
    Task<IResult> LoginAsync(StaffLoginRequest request);
}
