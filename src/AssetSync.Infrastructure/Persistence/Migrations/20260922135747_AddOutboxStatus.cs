using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_Attempts",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OutboxMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status",
                table: "OutboxMessages",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_Attempts",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "Attempts" });
        }
    }
}
