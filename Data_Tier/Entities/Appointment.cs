namespace Data_Tier.Entities;

public sealed class Appointment
{
    public long Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public DateTime StartsAt { get; set; }
    public required string CustomerName { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public required string IdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = "Confirmed";
}
