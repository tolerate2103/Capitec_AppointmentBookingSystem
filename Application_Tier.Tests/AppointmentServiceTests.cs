using Xunit;
using Application_Tier.Contracts;
using Application_Tier.Services;
using Data_Tier;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application_Tier.Tests;

public sealed class AppointmentServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppointmentDbContext _db = null!;
    private AppointmentService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppointmentDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppointmentDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _service = new AppointmentService(_db,
            Options.Create(new BookingOptions { SlotCapacity = 1, AdvanceBookingDays = 60 }),
            TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetBranches_ReturnsOnlyActiveBranches()
    {
        var branches = await _service.GetBranchesAsync(CancellationToken.None);
        Assert.NotEmpty(branches);
        Assert.Contains(branches, branch => branch.Name == "Sandton City");
    }

    [Fact]
    public async Task BookingSameIdempotencyKeyTwice_ReturnsOneSavedAppointment()
    {
        var request = CreateRequest("idempotency-key-000001");

        var first = await _service.BookAsync(request, CancellationToken.None);
        var retry = await _service.BookAsync(request, CancellationToken.None);

        Assert.True(first.IsNewBooking);
        Assert.False(retry.IsNewBooking);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, await _db.Appointments.CountAsync());
    }

    [Fact]
    public async Task BookingWhenSlotIsFull_ReturnsConflict()
    {
        await _service.BookAsync(CreateRequest("idempotency-key-000001"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            _service.BookAsync(CreateRequest("idempotency-key-000002"), CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(1, await _db.Appointments.CountAsync());
    }

    private static BookAppointmentRequest CreateRequest(string key)
    {
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(1);

        return new BookAppointmentRequest
        {
            BranchId = 1,
            Date = date.ToString("yyyy-MM-dd"),
            Time = "10:00",
            FullName = "Alex Morgan",
            Email = "alex@example.com",
            PhoneNumber = "082 123 4567",
            IdempotencyKey = key
        };
    }
}
