using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Application_Tier.Contracts;
using Data_Tier;
using Data_Tier.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Application_Tier.Services;

public sealed class AppointmentService(
    AppointmentDbContext db,
    IOptions<BookingOptions> options,
    TimeProvider timeProvider)
{
    private readonly BookingOptions _options = options.Value;
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    public async Task<IReadOnlyList<BranchResponse>> GetBranchesAsync(CancellationToken cancellationToken)
    {
        return await db.Branches.AsNoTracking()
            .Where(branch => branch.IsActive)
            .OrderBy(branch => branch.City).ThenBy(branch => branch.Name)
            .Select(branch => new BranchResponse(branch.Id, branch.Name, branch.Address, branch.City))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailabilityResponse>> GetAvailabilityAsync(int branchId, DateOnly date, CancellationToken cancellationToken)
    {
        ValidateDate(date);
        var branchExists = await db.Branches.AnyAsync(
            branch => branch.Id == branchId && branch.IsActive, cancellationToken);
        if (!branchExists)
            throw new BookingException("Please select an available branch.", StatusCodes.BadRequest);

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var bookedCounts = await db.Appointments.AsNoTracking()
            .Where(appointment => appointment.BranchId == branchId &&
                                  appointment.StartsAt >= dayStart &&
                                  appointment.StartsAt < dayStart.AddDays(1) &&
                                  appointment.Status == "Confirmed")
            .GroupBy(appointment => appointment.StartsAt)
            .Select(group => new { StartsAt = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.StartsAt, item => item.Count, cancellationToken);

        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone).DateTime;
        var slots = new List<AvailabilityResponse>();
        for (var hour = 9; hour <= 16; hour++)
        {
            foreach (var minute in new[] { 0, 30 })
            {
                var start = date.ToDateTime(new TimeOnly(hour, minute));
                if (start <= now || (hour == 16 && minute == 30))
                    continue;

                var booked = bookedCounts.GetValueOrDefault(start);
                var remaining = Math.Max(0, _options.SlotCapacity - booked);
                if (remaining > 0)
                    slots.Add(new AvailabilityResponse(start.ToString("HH:mm"), remaining, _options.SlotCapacity));
            }
        }

        return slots;
    }

    public async Task<AppointmentResponse> BookAsync(
        BookAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", out var date) ||
            !TimeOnly.TryParseExact(request.Time, "HH:mm", out var time))
            throw new BookingException("Choose a valid appointment date and time.", StatusCodes.BadRequest);

        ValidateDate(date);
        if (time.Minute is not (0 or 30) || time.Second != 0 || time.Hour is < 9 or > 16 ||
            (time.Hour == 16 && time.Minute == 30))
            throw new BookingException("That time is outside our appointment hours. Choose a slot between 09:00 and 16:00.", StatusCodes.BadRequest);

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = request.PhoneNumber.Trim();
        if (fullName.Length is < 2 or > 120)
            throw new BookingException("Enter a name between 2 and 120 characters.", StatusCodes.BadRequest);
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            throw new BookingException("Enter a valid email address.", StatusCodes.BadRequest);
        if (!Regex.IsMatch(phone, @"^(?=.*\d)\+?[0-9 ()-]{7,20}$"))
            throw new BookingException("Enter a valid contact number.", StatusCodes.BadRequest);
        if (request.IdempotencyKey.Length is < 16 or > 80)
            throw new BookingException("Please refresh the booking form and try again.", StatusCodes.BadRequest);

        // Return the original result for retries of a request that already committed.
        var existing = await db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Branch)
            .SingleOrDefaultAsync(appointment => appointment.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, request, date, time, fullName, email, phone);
            return ToResponse(existing, false);
        }

        var startsAt = date.ToDateTime(time);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var branch = await db.Branches.SingleOrDefaultAsync(
                item => item.Id == request.BranchId && item.IsActive, cancellationToken);
            if (branch is null)
                throw new BookingException("Please select an available branch.", StatusCodes.BadRequest);

            // Serializable isolation plus the (BranchId, StartsAt) index protects the capacity check
            // from concurrent inserts for this branch and time slot.
            var booked = await db.Appointments.CountAsync(appointment =>
                appointment.BranchId == request.BranchId &&
                appointment.StartsAt == startsAt &&
                appointment.Status == "Confirmed", cancellationToken);

            if (booked >= _options.SlotCapacity)
                throw new BookingException("Sorry, this time slot has just filled up. Please choose another time.", StatusCodes.Conflict);

            var appointment = new Appointment
            {
                BranchId = branch.Id,
                StartsAt = startsAt,
                CustomerName = fullName,
                Email = email,
                PhoneNumber = phone,
                IdempotencyKey = request.IdempotencyKey,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                Status = "Confirmed"
            };

            db.Appointments.Add(appointment);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            appointment.Branch = branch;
            return ToResponse(appointment, true);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();

            // If two retries arrive together, the unique idempotency index lets only one save.
            // Return that committed result rather than turning a harmless retry into a server error.
            var committed = await db.Appointments.AsNoTracking()
                .Include(appointment => appointment.Branch)
                .SingleOrDefaultAsync(appointment => appointment.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (committed is not null)
            {
                EnsureSameRequest(committed, request, date, time, fullName, email, phone);
                return ToResponse(committed, false);
            }

            if (exception.GetBaseException() is SqlException { Number: 1205 })
                throw new BookingException("Another booking was processed at the same time. Please check availability and try again.", StatusCodes.Conflict);

            throw;
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();

            var committed = await db.Appointments.AsNoTracking()
                .Include(appointment => appointment.Branch)
                .SingleOrDefaultAsync(appointment => appointment.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (committed is not null)
            {
                EnsureSameRequest(committed, request, date, time, fullName, email, phone);
                return ToResponse(committed, false);
            }

            throw new BookingException("Another booking was processed at the same time. Please check availability and try again.", StatusCodes.Conflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void EnsureSameRequest(
        Appointment existing,
        BookAppointmentRequest request,
        DateOnly date,
        TimeOnly time,
        string fullName,
        string email,
        string phone)
    {
        if (existing.BranchId != request.BranchId || existing.StartsAt != date.ToDateTime(time) ||
            existing.CustomerName != fullName || existing.Email != email || existing.PhoneNumber != phone)
            throw new BookingException("This submission key has already been used for a different appointment. Please start a new booking.", StatusCodes.Conflict);
    }

    private void ValidateDate(DateOnly date)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        if (date < today)
            throw new BookingException("Appointments cannot be booked in the past.", StatusCodes.BadRequest);
        if (date > today.AddDays(_options.AdvanceBookingDays))
            throw new BookingException($"Appointments can be booked up to {_options.AdvanceBookingDays} days in advance.", StatusCodes.BadRequest);
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            throw new BookingException("Branches are available for appointments Monday to Friday.", StatusCodes.BadRequest);
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
        }
    }

    private static AppointmentResponse ToResponse(Appointment appointment, bool isNewBooking) => new(
        appointment.Id,
        $"APT-{appointment.Id:000000}",
        appointment.Branch?.Name ?? "Branch",
        appointment.Branch?.Address ?? string.Empty,
        appointment.CustomerName,
        appointment.Email,
        appointment.PhoneNumber,
        appointment.StartsAt,
        appointment.Status,
        isNewBooking);

    private static class StatusCodes
    {
        public const int BadRequest = 400;
        public const int Conflict = 409;
    }
}
