namespace Application_Tier.Contracts;

public sealed record AvailabilityResponse(string Time, int RemainingCapacity, int Capacity);
