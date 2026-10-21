using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TourismHcm.Infrastructure.Persistence;

#nullable disable

namespace TourismHcm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(TourismHcmDbContext))]
    [Migration("20261018100600_FullParityPlatform")]
    public partial class FullParityPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlaceOwnerVerified",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdEncrypted",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnedPlaceId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PiiRedactedAt",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermissionsJson",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Roles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Places",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_OwnedPlaceId",
                table: "Users",
                column: "OwnedPlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Places_OwnerUserId",
                table: "Places",
                column: "OwnerUserId");

            migrationBuilder.CreateTable(
                name: "AnalyticsDailyMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "date", nullable: false),
                    MetricKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsDailyMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceKeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConsentAnalytics = table.Column<bool>(type: "bit", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsHourlyMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HourUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MetricKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsHourlyMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeLocationHourlies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HourUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BucketKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeLocationHourlies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UiTranslationBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Namespace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiTranslationBundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiUsageLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "date", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsageLimits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OwnerNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RelatedSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnerNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OwnerRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NationalIdEncrypted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnerRegistrations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaceSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaceSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaceSubmissions_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlaceSubmissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsentAnalytics = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsSessions_AnalyticsDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AnalyticsDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsentAnalytics = table.Column<bool>(type: "bit", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsEvents_AnalyticsSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AnalyticsSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLimits_UserId_DateUtc",
                table: "AiUsageLimits",
                columns: new[] { "UserId", "DateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDailyMetrics_DateUtc_MetricKey",
                table: "AnalyticsDailyMetrics",
                columns: new[] { "DateUtc", "MetricKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDevices_DeviceKeyHash",
                table: "AnalyticsDevices",
                column: "DeviceKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_OccurredAt",
                table: "AnalyticsEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_SessionId",
                table: "AnalyticsEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsHourlyMetrics_HourUtc_MetricKey",
                table: "AnalyticsHourlyMetrics",
                columns: new[] { "HourUtc", "MetricKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSessions_DeviceId",
                table: "AnalyticsSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSessions_StartedAt",
                table: "AnalyticsSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerNotifications_UserId",
                table: "OwnerNotifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerNotifications_UserId_IsRead",
                table: "OwnerNotifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_OwnerRegistrations_Status",
                table: "OwnerRegistrations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerRegistrations_UserId",
                table: "OwnerRegistrations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaceSubmissions_PlaceId",
                table: "PlaceSubmissions",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaceSubmissions_Status",
                table: "PlaceSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PlaceSubmissions_UserId",
                table: "PlaceSubmissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeLocationHourlies_HourUtc_BucketKey",
                table: "RuntimeLocationHourlies",
                columns: new[] { "HourUtc", "BucketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UiTranslationBundles_Namespace_Locale_SourceHash",
                table: "UiTranslationBundles",
                columns: new[] { "Namespace", "Locale", "SourceHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageLimits");

            migrationBuilder.DropTable(
                name: "AnalyticsDailyMetrics");

            migrationBuilder.DropTable(
                name: "AnalyticsEvents");

            migrationBuilder.DropTable(
                name: "AnalyticsHourlyMetrics");

            migrationBuilder.DropTable(
                name: "OwnerNotifications");

            migrationBuilder.DropTable(
                name: "OwnerRegistrations");

            migrationBuilder.DropTable(
                name: "PlaceSubmissions");

            migrationBuilder.DropTable(
                name: "RuntimeLocationHourlies");

            migrationBuilder.DropTable(
                name: "UiTranslationBundles");

            migrationBuilder.DropTable(
                name: "AnalyticsSessions");

            migrationBuilder.DropTable(
                name: "AnalyticsDevices");

            migrationBuilder.DropIndex(
                name: "IX_Users_OwnedPlaceId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Places_OwnerUserId",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "IsPlaceOwnerVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NationalIdEncrypted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OwnedPlaceId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PiiRedactedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PermissionsJson",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Places");
        }
    }
}
