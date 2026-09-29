using Cinema.Foundation.Security;
using Identity.Api.Models;
using Identity.Api.Repositories;

namespace Identity.Api.Services;

public class SupervisorService : ISupervisorService
{
    private readonly IIdentityRepository _repository;

    public SupervisorService(IIdentityRepository repository)
    {
        _repository = repository;
    }

    public async Task<IResult> VerifySupervisorPinAsync(SupervisorVerifyRequest request)
    {
        var supervisors = (await _repository.GetSupervisorsAsync(request.SupervisorUsername)).ToList();
        foreach (var sup in supervisors)
        {
            string hash = sup.pin_hash ?? "";
            if (PasswordHasher.Verify(request.SupervisorPin, hash))
            {
                return Results.Ok(new
                {
                    verified = true,
                    supervisorId = (Guid)sup.user_id,
                    supervisorUsername = (string)sup.username,
                    displayName = (string)sup.display_name
                });
            }
        }

        return Results.Json(new { verified = false, error = "Invalid supervisor PIN" }, statusCode: StatusCodes.Status401Unauthorized);
    }
}
