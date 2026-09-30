using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ClosedServices_Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "closedservices");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "services",
                schema: "closedservices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_data_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_type = table.Column<int>(type: "integer", nullable: false),
                    short_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    postcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    geom = table.Column<Point>(type: "geometry", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_services", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_operating_days",
                schema: "closedservices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_operating_days", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_operating_days_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "closedservices",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_status_updates",
                schema: "closedservices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closure_state = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_external_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_status_updates", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_status_updates_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "closedservices",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_service_permissions",
                schema: "closedservices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_type = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_service_permissions", x => x.id);
                    table.CheckConstraint("ck_userservicepermission_target", "(service_id IS NOT NULL AND service_type IS NULL)\r\nOR (service_id IS NULL AND service_type IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_user_service_permissions_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "closedservices",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_operating_days_service_id_day_of_week",
                schema: "closedservices",
                table: "service_operating_days",
                columns: new[] { "service_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_status_updates_service_id_updated_at",
                schema: "closedservices",
                table: "service_status_updates",
                columns: new[] { "service_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_service_permissions_external_user_id",
                schema: "closedservices",
                table: "user_service_permissions",
                column: "external_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_service_permissions_external_user_id_service_id",
                schema: "closedservices",
                table: "user_service_permissions",
                columns: new[] { "external_user_id", "service_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_service_permissions_external_user_id_service_type",
                schema: "closedservices",
                table: "user_service_permissions",
                columns: new[] { "external_user_id", "service_type" });

            migrationBuilder.CreateIndex(
                name: "ix_user_service_permissions_service_id",
                schema: "closedservices",
                table: "user_service_permissions",
                column: "service_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_operating_days",
                schema: "closedservices");

            migrationBuilder.DropTable(
                name: "service_status_updates",
                schema: "closedservices");

            migrationBuilder.DropTable(
                name: "user_service_permissions",
                schema: "closedservices");

            migrationBuilder.DropTable(
                name: "services",
                schema: "closedservices");
        }
    }
}
