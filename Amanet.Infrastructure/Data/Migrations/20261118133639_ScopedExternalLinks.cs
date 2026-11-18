using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScopedExternalLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatterId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Progress = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "integer", nullable: true),
                    SignedDocumentVersionId = table.Column<int>(type: "integer", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalRequests_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalRequests_DocumentVersions_SignedDocumentVersionId",
                        column: x => x.SignedDocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalRequests_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Template = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDeliveryAttempts_ExternalRequests_ExternalRequestId",
                        column: x => x.ExternalRequestId,
                        principalTable: "ExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryAttempts_ExternalRequestId_StartedAtUtc",
                table: "EmailDeliveryAttempts",
                columns: new[] { "ExternalRequestId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_DocumentVersionId",
                table: "ExternalRequests",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_MatterId",
                table: "ExternalRequests",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_SignedDocumentVersionId",
                table: "ExternalRequests",
                column: "SignedDocumentVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "ExternalRequests");
        }
    }
}
