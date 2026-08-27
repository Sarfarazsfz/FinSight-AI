using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reconciliation_results_run_id",
                table: "reconciliation_results");

            migrationBuilder.DropIndex(
                name: "IX_reconciliation_exceptions_run_id",
                table: "reconciliation_exceptions");

            migrationBuilder.CreateIndex(
                name: "IX_Settlement_Batch_TransactionReference",
                table: "settlement_records",
                columns: new[] { "batch_id", "transaction_reference" });

            migrationBuilder.CreateIndex(
                name: "IX_Result_Run_CreatedAt",
                table: "reconciliation_results",
                columns: new[] { "run_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_Exception_Run_CreatedAt",
                table: "reconciliation_exceptions",
                columns: new[] { "run_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Batch_TransactionReference",
                table: "payment_records",
                columns: new[] { "batch_id", "transaction_reference" });

            migrationBuilder.CreateIndex(
                name: "IX_Bank_Batch_TransactionReference",
                table: "bank_records",
                columns: new[] { "batch_id", "transaction_reference" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Settlement_Batch_TransactionReference",
                table: "settlement_records");

            migrationBuilder.DropIndex(
                name: "IX_Result_Run_CreatedAt",
                table: "reconciliation_results");

            migrationBuilder.DropIndex(
                name: "IX_Exception_Run_CreatedAt",
                table: "reconciliation_exceptions");

            migrationBuilder.DropIndex(
                name: "IX_Payment_Batch_TransactionReference",
                table: "payment_records");

            migrationBuilder.DropIndex(
                name: "IX_Bank_Batch_TransactionReference",
                table: "bank_records");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_results_run_id",
                table: "reconciliation_results",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_exceptions_run_id",
                table: "reconciliation_exceptions",
                column: "run_id");
        }
    }
}
