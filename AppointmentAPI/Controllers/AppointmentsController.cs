using System.Globalization;
using Application_Tier.Contracts;
using Application_Tier.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentAPI.Controllers;

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController(AppointmentService appointments) : ControllerBase
{
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability([FromQuery] int branchId, [FromQuery] string date, CancellationToken cancellationToken)
    {
        if (branchId <= 0 || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var appointmentDate))
            return BadRequest(new { message = "Choose a branch and a valid appointment date." });

        return Ok(await appointments.GetAvailabilityAsync(branchId, appointmentDate, cancellationToken));
    }


    [HttpPost]
    public async Task<IActionResult> Book([FromBody] BookAppointmentRequest request, CancellationToken cancellationToken)
    {
        var appointment = await appointments.BookAsync(request, cancellationToken);
        return appointment.IsNewBooking ? StatusCode(StatusCodes.Status201Created, appointment)
            : Ok(appointment);
    }

}
