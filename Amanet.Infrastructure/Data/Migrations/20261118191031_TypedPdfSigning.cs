using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TypedPdfSigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentVersions_Length",
                table: "DocumentVersions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SignedAtUtc",
                table: "MatterDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdminSignerId",
                table: "ExternalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReplacedAtUtc",
                table: "ExternalRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignerKind",
                table: "ExternalRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SigningConsentText",
                table: "ExternalRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SigningDocumentId",
                table: "ExternalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                table: "ExternalRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ViewedAtUtc",
                table: "ExternalRequests",
                type: "timestamp with time zone",
                nullable: true);

            // Preserve existing invitation links where possible. A logical document gets one current
            // owner, preferring a completed workflow and otherwise its newest invitation.
            migrationBuilder.Sql("""
                UPDATE "ExternalRequests"
                SET "SignerKind" = 'ExternalClient',
                    "SigningConsentText" = CASE WHEN "Progress" <> 'Completed'
                        THEN 'I have reviewed this document and agree to sign it electronically using my typed name.' END,
                    "ViewedAtUtc" = CASE WHEN "Progress" IN ('Viewed', 'Started', 'Completed') THEN "CreatedAtUtc" END,
                    "StartedAtUtc" = CASE WHEN "Progress" IN ('Started', 'Completed') THEN "CreatedAtUtc" END,
                    "Progress" = CASE "Progress" WHEN 'Pending' THEN 'Sent' WHEN 'Completed' THEN 'Signed' ELSE "Progress" END
                WHERE "Action" = 'Sign';

                WITH owners AS (
                    SELECT request."Id", version."DocumentId",
                           row_number() OVER (PARTITION BY version."DocumentId"
                               ORDER BY (request."Progress" = 'Signed') DESC,
                                        (request."RevokedAtUtc" IS NULL AND request."ExpiresAtUtc" > CURRENT_TIMESTAMP) DESC,
                                        request."CreatedAtUtc" DESC, request."Id") AS slot_order
                    FROM "ExternalRequests" request
                    JOIN "DocumentVersions" version ON version."Id" = request."DocumentVersionId"
                    WHERE request."Action" = 'Sign'
                )
                UPDATE "ExternalRequests" request
                SET "SigningDocumentId" = CASE WHEN owners.slot_order = 1 THEN owners."DocumentId" END,
                    "ReplacedAtUtc" = CASE WHEN owners.slot_order > 1 THEN CURRENT_TIMESTAMP END,
                    "RevokedAtUtc" = CASE WHEN owners.slot_order > 1 THEN COALESCE(request."RevokedAtUtc", CURRENT_TIMESTAMP) ELSE request."RevokedAtUtc" END
                FROM owners WHERE request."Id" = owners."Id";

                UPDATE "MatterDocuments" document
                SET "SignedAtUtc" = COALESCE(request."CompletedAtUtc", request."CreatedAtUtc")
                FROM "ExternalRequests" request
                WHERE request."SigningDocumentId" = document."Id" AND request."Progress" = 'Signed';
                """);

            migrationBuilder.CreateTable(
                name: "SignedDocuments",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    SourceVersionId = table.Column<int>(type: "integer", nullable: false),
                    FinalVersionId = table.Column<int>(type: "integer", nullable: false),
                    SignerKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignerEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    AdminSignerId = table.Column<int>(type: "integer", nullable: true),
                    SignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsentText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignedDocuments", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_SignedDocuments_AspNetUsers_AdminSignerId",
                        column: x => x.AdminSignerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignedDocuments_DocumentVersions_FinalVersionId",
                        column: x => x.FinalVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignedDocuments_DocumentVersions_SourceVersionId",
                        column: x => x.SourceVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignedDocuments_ExternalRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignedDocuments_MatterDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "MatterDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_AdminSignerId",
                table: "ExternalRequests",
                column: "AdminSignerId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRequests_SigningDocumentId",
                table: "ExternalRequests",
                column: "SigningDocumentId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentVersions_Length",
                table: "DocumentVersions",
                sql: "\"ByteLength\" > 0 AND \"ByteLength\" <= 26214400");

            migrationBuilder.CreateIndex(
                name: "IX_SignedDocuments_AdminSignerId",
                table: "SignedDocuments",
                column: "AdminSignerId");

            migrationBuilder.CreateIndex(
                name: "IX_SignedDocuments_DocumentId",
                table: "SignedDocuments",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignedDocuments_FinalVersionId",
                table: "SignedDocuments",
                column: "FinalVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignedDocuments_SourceVersionId",
                table: "SignedDocuments",
                column: "SourceVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalRequests_AspNetUsers_AdminSignerId",
                table: "ExternalRequests",
                column: "AdminSignerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalRequests_MatterDocuments_SigningDocumentId",
                table: "ExternalRequests",
                column: "SigningDocumentId",
                principalTable: "MatterDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ExternalRequests"
                SET "Progress" = CASE "Progress" WHEN 'Sent' THEN 'Pending' WHEN 'Signed' THEN 'Completed' ELSE "Progress" END
                WHERE "Action" = 'Sign';
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalRequests_AspNetUsers_AdminSignerId",
                table: "ExternalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalRequests_MatterDocuments_SigningDocumentId",
                table: "ExternalRequests");

            migrationBuilder.DropTable(
                name: "SignedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_AdminSignerId",
                table: "ExternalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ExternalRequests_SigningDocumentId",
                table: "ExternalRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentVersions_Length",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "SignedAtUtc",
                table: "MatterDocuments");

            migrationBuilder.DropColumn(
                name: "AdminSignerId",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "ReplacedAtUtc",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "SignerKind",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "SigningConsentText",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "SigningDocumentId",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "ExternalRequests");

            migrationBuilder.DropColumn(
                name: "ViewedAtUtc",
                table: "ExternalRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentVersions_Length",
                table: "DocumentVersions",
                sql: "\"ByteLength\" > 0 AND \"ByteLength\" <= 20971520");
        }
    }
}
