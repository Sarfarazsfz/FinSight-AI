using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAndSettlementStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "settlement_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "bank_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "settlement_records");

            migrationBuilder.DropColumn(
                name: "status",
                table: "bank_records");
        }
    }
}
