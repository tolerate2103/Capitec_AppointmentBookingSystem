using Data_Tier.Entities;
using Microsoft.EntityFrameworkCore;

namespace Data_Tier;

public sealed class AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : DbContext(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("Branches");
            entity.HasKey(branch => branch.Id);
            entity.Property(branch => branch.Name).HasMaxLength(100).IsRequired();
            entity.Property(branch => branch.Address).HasMaxLength(200).IsRequired();
            entity.Property(branch => branch.City).HasMaxLength(80).IsRequired();
            entity.HasData(
                new Branch { Id = 1, Name = "Sandton City", Address = "Shop L24, Sandton City, Rivonia Road", City = "Johannesburg", IsActive = true },
                new Branch { Id = 2, Name = "Rosebank Mall", Address = "Shop 204, Rosebank Mall, Cradock Avenue", City = "Johannesburg", IsActive = true },
                new Branch { Id = 3, Name = "Menlyn Park", Address = "Shop 112, Menlyn Park, Atterbury Road", City = "Pretoria", IsActive = true },
                new Branch { Id = 4, Name = "Gateway Theatre", Address = "Shop F128, Gateway Theatre of Shopping", City = "Durban", IsActive = true });
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("Appointments");
            entity.HasKey(appointment => appointment.Id);
            entity.Property(appointment => appointment.CustomerName).HasMaxLength(120).IsRequired();
            entity.Property(appointment => appointment.Email).HasMaxLength(254).IsRequired();
            entity.Property(appointment => appointment.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(appointment => appointment.IdempotencyKey).HasMaxLength(80).IsRequired();
            entity.Property(appointment => appointment.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(appointment => new { appointment.BranchId, appointment.StartsAt });
            entity.HasIndex(appointment => appointment.IdempotencyKey).IsUnique();
            entity.HasOne(appointment => appointment.Branch)
                .WithMany(branch => branch.Appointments)
                .HasForeignKey(appointment => appointment.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
