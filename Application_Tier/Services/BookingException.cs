namespace Application_Tier.Services;

public sealed class BookingException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
