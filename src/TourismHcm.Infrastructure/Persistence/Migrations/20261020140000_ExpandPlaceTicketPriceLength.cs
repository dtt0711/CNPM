using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TourismHcm.Infrastructure.Persistence;

#nullable disable

namespace TourismHcm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TourismHcmDbContext))]
[Migration("20261020140000_ExpandPlaceTicketPriceLength")]
public partial class ExpandPlaceTicketPriceLength : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "TicketPrice",
            table: "Places",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "TicketPrice",
            table: "Places",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(1000)",
            oldMaxLength: 1000);
    }
}
