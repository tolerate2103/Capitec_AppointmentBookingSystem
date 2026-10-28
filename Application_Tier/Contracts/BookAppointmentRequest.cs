using System.ComponentModel.DataAnnotations;

namespace Application_Tier.Contracts;

public sealed class BookAppointmentRequest
{
    [Range(1, int.MaxValue)]
    public int BranchId { get; init; }

    [Required]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Choose a valid appointment date.")]
    public string Date { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^(09|10|11|12|13|14|15|16):(00|30)$", ErrorMessage = "Choose an available half-hour time slot.")]
    public string Time { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression(@"^(?=.*\d)\+?[0-9 ()-]{7,20}$", ErrorMessage = "Enter a valid contact number.")]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 16)]
    public string IdempotencyKey { get; init; } = string.Empty;
}
