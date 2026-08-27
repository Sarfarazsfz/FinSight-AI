using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_record_count = table.Column<int>(type: "integer", nullable: false),
                    bank_record_count = table.Column<int>(type: "integer", nullable: false),
                    settlement_record_count = table.Column<int>(type: "integer", nullable: false),
                    total_record_count = table.Column<int>(type: "integer", nullable: false),
                    validation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches", x => x.id);
                    table.CheckConstraint("CHK_Batch_BankRecordCount", "\"bank_record_count\" >= 0");
                    table.CheckConstraint("CHK_Batch_PaymentRecordCount", "\"payment_record_count\" >= 0");
                    table.CheckConstraint("CHK_Batch_SettlementRecordCount", "\"settlement_record_count\" >= 0");
                    table.CheckConstraint("CHK_Batch_TotalRecordCount", "\"total_record_count\" >= 0");
                    table.CheckConstraint("CHK_Batch_ValidationStatus", "\"validation_status\" IN ('Valid', 'Invalid')");
                });

            migrationBuilder.CreateTable(
                name: "bank_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_record_identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_records_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_record_identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_records_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_reconciliation_units = table.Column<int>(type: "integer", nullable: false),
                    match_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => x.id);
                    table.CheckConstraint("CHK_Run_MatchRate", "\"match_rate\" IS NULL OR (\"match_rate\" >= 0 AND \"match_rate\" <= 100)");
                    table.CheckConstraint("CHK_Run_Status", "\"status\" IN ('Pending', 'Running', 'Completed', 'Failed')");
                    table.CheckConstraint("CHK_Run_TotalUnits", "\"total_reconciliation_units\" >= 0");
                    table.ForeignKey(
                        name: "FK_reconciliation_runs_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "settlement_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_record_identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_settlement_records_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    detail_payload = table.Column<string>(type: "jsonb", nullable: false),
                    related_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.CheckConstraint("CHK_Audit_EventType", "\"event_type\" IN ('BatchCreated', 'BatchValidated', 'ReconciliationStarted', 'ReconciliationCompleted', 'ReconciliationFailed', 'ReconciliationDecisionRecorded', 'ExceptionCreated', 'AiQuestionAsked', 'AiToolInvoked', 'AiExplanationRequested', 'AiExplanationFailed', 'AiAssistantFailed')");
                    table.CheckConstraint("CHK_Audit_RelatedEntityPair", "(\"related_entity_type\" IS NULL AND \"related_entity_id\" IS NULL) OR (\"related_entity_type\" IS NOT NULL AND \"related_entity_id\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_audit_logs_reconciliation_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "reconciliation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "normalized_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settlement_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_normalized_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_normalized_transactions_bank_records_bank_record_id",
                        column: x => x.bank_record_id,
                        principalTable: "bank_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_normalized_transactions_payment_records_payment_record_id",
                        column: x => x.payment_record_id,
                        principalTable: "payment_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_normalized_transactions_reconciliation_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "reconciliation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_normalized_transactions_settlement_records_settlement_recor~",
                        column: x => x.settlement_record_id,
                        principalTable: "settlement_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    strategy_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_results", x => x.id);
                    table.CheckConstraint("CHK_Result_Status", "\"status\" IN ('Matched', 'Mismatched', 'Missing', 'Duplicate', 'Unresolved')");
                    table.ForeignKey(
                        name: "FK_reconciliation_results_normalized_transactions_normalized_t~",
                        column: x => x.normalized_transaction_id,
                        principalTable: "normalized_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reconciliation_results_reconciliation_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "reconciliation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    involved_sources = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    discrepancy_detail = table.Column<string>(type: "jsonb", nullable: false),
                    ai_explanation = table.Column<string>(type: "text", nullable: true),
                    ai_suggested_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ai_explanation_generated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_exceptions", x => x.id);
                    table.CheckConstraint("CHK_Exception_Category", "\"category\" IN ('AmountMismatch', 'DateMismatch', 'MissingRecord', 'DuplicateRecord', 'Unresolved')");
                    table.ForeignKey(
                        name: "FK_reconciliation_exceptions_reconciliation_results_reconcilia~",
                        column: x => x.reconciliation_result_id,
                        principalTable: "reconciliation_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reconciliation_exceptions_reconciliation_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "reconciliation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_run_id",
                table: "audit_logs",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Bank_TechDup",
                table: "bank_records",
                columns: new[] { "batch_id", "source_record_identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_normalized_transactions_bank_record_id",
                table: "normalized_transactions",
                column: "bank_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_normalized_transactions_payment_record_id",
                table: "normalized_transactions",
                column: "payment_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_normalized_transactions_settlement_record_id",
                table: "normalized_transactions",
                column: "settlement_record_id");

            migrationBuilder.CreateIndex(
                name: "UQ_NormalizedTx_Ref",
                table: "normalized_transactions",
                columns: new[] { "run_id", "transaction_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Payment_TechDup",
                table: "payment_records",
                columns: new[] { "batch_id", "source_record_identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_exceptions_run_id",
                table: "reconciliation_exceptions",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Exception_Result",
                table: "reconciliation_exceptions",
                column: "reconciliation_result_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_results_run_id",
                table: "reconciliation_results",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Result_Tx",
                table: "reconciliation_results",
                column: "normalized_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_batch_id",
                table: "reconciliation_runs",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Settlement_TechDup",
                table: "settlement_records",
                columns: new[] { "batch_id", "source_record_identifier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "reconciliation_exceptions");

            migrationBuilder.DropTable(
                name: "reconciliation_results");

            migrationBuilder.DropTable(
                name: "normalized_transactions");

            migrationBuilder.DropTable(
                name: "bank_records");

            migrationBuilder.DropTable(
                name: "payment_records");

            migrationBuilder.DropTable(
                name: "reconciliation_runs");

            migrationBuilder.DropTable(
                name: "settlement_records");

            migrationBuilder.DropTable(
                name: "batches");
        }
    }
}
