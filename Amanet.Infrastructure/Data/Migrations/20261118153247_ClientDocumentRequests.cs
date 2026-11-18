using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Amanet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClientDocumentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_MatterId",
                table: "ExternalRequests");

            migrationBuilder.AddColumn<string>(
                name: "Classification",
                table: "MatterDocuments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "ExternalRequests",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "ExternalRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UploadStatus",
                table: "ExternalRequests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UploadedDocumentId",
                table: "ExternalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UploaderId",
                table: "DocumentVersions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // Keep existing link state while assigning the new upload workflow status.
            migrationBuilder.Sql("""
                UPDATE "ExternalRequests"
                SET "UploadStatus" = CASE WHEN "Progress" = 'Completed' THEN 'Accepted' ELSE 'AwaitingUpload' END
                WHERE "Action" = 'Upload';
                """);

            migrationBuilder.CreateTable(
                name: "AdminNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatterId = table.Column<int>(type: "integer", nullable: false),
                    ExternalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminNotifications_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminNotifications_ExternalRequests_ExternalRequestId",
                        column: x => x.ExternalRequestId,
                        principalTable: "ExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminNotifications_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSubmissions",
                columns: table => new
                {
                    DocumentVersionId = table.Column<int>(type: "integer", nullable: false),
                    ExternalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CorrectionExplanation = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ReviewerId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSubmissions", x => x.DocumentVersionId);
                    table.CheckConstraint("CK_DocumentSubmissions_Review", "\"Decision\" <> 'CorrectionsRequested' OR (\"CorrectionExplanation\" IS NOT NULL AND length(btrim(\"CorrectionExplanation\")) > 0)");
                    table.ForeignKey(
                        name: "FK_DocumentSubmissions_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentSubmissions_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentSubmissions_ExternalRequests_ExternalRequestId",
                        column: x => x.ExternalRequestId,
                        principalTable: "ExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_MatterId_UploadStatus",
                table: "ExternalRequests",
                columns: new[] { "MatterId", "UploadStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_UploadedDocumentId",
                table: "ExternalRequests",
                column: "UploadedDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_CreatedAtUtc",
                table: "AdminNotifications",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_DocumentVersionId",
                table: "AdminNotifications",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_ExternalRequestId",
                table: "AdminNotifications",
                column: "ExternalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_MatterId",
                table: "AdminNotifications",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSubmissions_ExternalRequestId",
                table: "DocumentSubmissions",
                column: "ExternalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSubmissions_ReviewerId",
                table: "DocumentSubmissions",
                column: "ReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalRequests_MatterDocuments_UploadedDocumentId",
                table: "ExternalRequests",
                column: "UploadedDocumentId",
                principalTable: "MatterDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalRequests_MatterDocuments_UploadedDocumentId",
                table: "ExternalRequests");

            migrationBuilder.DropTable(
                name: "AdminNotifications");

            migrationBuilder.DropTable(
                name: "DocumentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_MatterId_UploadStatus",
                table: "ExternalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_UploadedDocumentId",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "Classification",
                table: "MatterDocuments");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "UploadStatus",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "UploadedDocumentId",
                table: "ExternalRequests");

            migrationBuilder.AlterColumn<int>(
                name: "UploaderId",
                table: "DocumentVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_MatterId",
                table: "ExternalRequests",
                column: "MatterId");
        }
    }
}
