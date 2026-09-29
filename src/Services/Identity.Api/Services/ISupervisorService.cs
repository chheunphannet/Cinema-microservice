using Identity.Api.Models;

namespace Identity.Api.Services;

public interface ISupervisorService
{
    Task<IResult> VerifySupervisorPinAsync(SupervisorVerifyRequest request);
}
