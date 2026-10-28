using Data_Tier;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data_Tier.Migrations;

[DbContext(typeof(AppointmentDbContext))]
[Migration("202610260001_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Branches",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Branches", branch => branch.Id));

        migrationBuilder.CreateTable(
            name: "Appointments",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                BranchId = table.Column<int>(type: "int", nullable: false),
                StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Appointments", appointment => appointment.Id);
                table.ForeignKey("FK_Appointments_Branches_BranchId", appointment => appointment.BranchId,
                    "Branches", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_Appointments_BranchId_StartsAt", "Appointments", new[] { "BranchId", "StartsAt" });
        migrationBuilder.CreateIndex("IX_Appointments_IdempotencyKey", "Appointments", "IdempotencyKey", unique: true);


        migrationBuilder.InsertData(
            table: "Branches",
            columns: new[] { "Id", "Address", "City", "IsActive", "Name" },
            columnTypes: new[]
            {
            "int",
            "nvarchar(200)",
            "nvarchar(80)",
            "bit",
            "nvarchar(100)"
            },
            values: new object[,]
            {
        { 1, "Shop L24, Sandton City, Rivonia Road", "Johannesburg", true, "Sandton City" },
        { 2, "Shop 204, Rosebank Mall, Cradock Avenue", "Johannesburg", true, "Rosebank Mall" },
        { 3, "Shop 112, Menlyn Park, Atterbury Road", "Pretoria", true, "Menlyn Park" },
        { 4, "Shop F128, Gateway Theatre of Shopping", "Durban", true, "Gateway Theatre" }
            });

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Appointments");
        migrationBuilder.DropTable(name: "Branches");
    }
}
