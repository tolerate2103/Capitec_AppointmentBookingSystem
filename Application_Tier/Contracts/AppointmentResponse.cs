namespace Application_Tier.Contracts;

public sealed record AppointmentResponse(
    long Id,
    string Reference,
    string BranchName,
    string BranchAddress,
    string CustomerName,
    string Email,
    string PhoneNumber,
    DateTime StartsAt,
    string Status,
    bool IsNewBooking);
