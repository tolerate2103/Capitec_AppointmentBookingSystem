using Application_Tier.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentAPI.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(AppointmentService appointments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await appointments.GetBranchesAsync(cancellationToken));
}
