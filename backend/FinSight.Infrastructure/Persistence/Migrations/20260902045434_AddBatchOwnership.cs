using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_batches_created_by_user_id",
                table: "batches",
                column: "created_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_batches_users_created_by_user_id",
                table: "batches",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Safe backfill, not an invented assignment: a batch's
            // pre-existing created_by (an email string) is correlated to
            // a real registered user only when that email genuinely
            // matches one -- both sides are lowercased/trimmed
            // defensively even though users.email is already normalized
            // that way (CHK_User_Email_Lowercase), since created_by has
            // no such constraint.
            //
            // A batch whose created_by does not match any current user
            // (a deleted account, or a label like "pagination-test" from
            // a test/dev fixture) is deliberately left NULL rather than
            // assigned to an arbitrary user. Under the new ownership
            // check, NULL means inaccessible to everyone -- a safe
            // default-deny, never a false grant, and never a destructive
            // change to the row itself.
            migrationBuilder.Sql(
                """
                UPDATE batches
                SET created_by_user_id = u.id
                FROM users u
                WHERE lower(trim(batches.created_by)) = u.email
                  AND batches.created_by_user_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_batches_users_created_by_user_id",
                table: "batches");

            migrationBuilder.DropIndex(
                name: "IX_batches_created_by_user_id",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "batches");
        }
    }
}
