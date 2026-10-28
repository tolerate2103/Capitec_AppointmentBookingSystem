using Application_Tier.Services;
using Data_Tier;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(item => item.Key, item => item.Value!.Errors.Select(error =>
                string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage).ToArray());
        return new BadRequestObjectResult(new { message = "Please check the highlighted details.", errors });
    };
});

builder.Services.AddOptions<BookingOptions>()
    .Bind(builder.Configuration.GetSection("Booking"))
    .Validate(options => options.SlotCapacity > 0, "Booking:SlotCapacity must be greater than zero.")
    .Validate(options => options.AdvanceBookingDays is >= 1 and <= 365, "Booking:AdvanceBookingDays must be between 1 and 365.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AppointmentService>();

if (builder.Environment.IsDevelopment())
    AppDomain.CurrentDomain.SetData("DataDirectory", Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..")));


var connectionString = builder.Configuration.GetConnectionString("AppointmentDb")
    ?? throw new InvalidOperationException("ConnectionStrings:AppointmentDb must be configured.");
builder.Services.AddDbContext<AppointmentDbContext>(options => options.UseSqlServer(connectionString));


var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BookingException exception)
    {
        context.Response.StatusCode = exception.StatusCode;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message });
    }
    catch (Exception exception) when (!context.Response.HasStarted)
    {
        app.Logger.LogError(exception, "An unexpected error occurred while processing {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { message = "Something went wrong. Please try again." });
    }
});


// Apply schema migrations before accepting requests. For multi-instance production deployments,
// run migrations as a controlled release step rather than from every instance.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline. Angular's local proxy targets this API over HTTP on port 5095.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // In the Docker/production deployment, the Angular production build is copied to wwwroot.
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();
