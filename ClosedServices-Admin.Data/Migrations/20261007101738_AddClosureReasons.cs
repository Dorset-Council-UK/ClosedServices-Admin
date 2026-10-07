using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClosedServices_Admin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClosureReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "closure_reason_id",
                schema: "closedservices",
                table: "service_status_updates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "closure_reasons",
                schema: "closedservices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_closure_reasons", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_status_updates_closure_reason_id",
                schema: "closedservices",
                table: "service_status_updates",
                column: "closure_reason_id");

            migrationBuilder.AddForeignKey(
                name: "fk_service_status_updates_closure_reasons_closure_reason_id",
                schema: "closedservices",
                table: "service_status_updates",
                column: "closure_reason_id",
                principalSchema: "closedservices",
                principalTable: "closure_reasons",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_service_status_updates_closure_reasons_closure_reason_id",
                schema: "closedservices",
                table: "service_status_updates");

            migrationBuilder.DropTable(
                name: "closure_reasons",
                schema: "closedservices");

            migrationBuilder.DropIndex(
                name: "ix_service_status_updates_closure_reason_id",
                schema: "closedservices",
                table: "service_status_updates");

            migrationBuilder.DropColumn(
                name: "closure_reason_id",
                schema: "closedservices",
                table: "service_status_updates");
        }
    }
}
