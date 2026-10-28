namespace Application_Tier.Services;

public sealed class BookingOptions
{
    public int SlotCapacity { get; set; } = 4;
    public int AdvanceBookingDays { get; set; } = 60;
}
