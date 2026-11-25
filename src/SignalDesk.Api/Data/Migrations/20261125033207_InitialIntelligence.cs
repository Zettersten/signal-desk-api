using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalDesk.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261125033207_InitialIntelligence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiExecutions",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Provider = table.Column<string>(type: "TEXT", nullable: false),
                Operation = table.Column<string>(type: "TEXT", nullable: false),
                Model = table.Column<string>(type: "TEXT", nullable: false),
                Version = table.Column<string>(type: "TEXT", nullable: false),
                InputHash = table.Column<string>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                ReservedUsd = table.Column<decimal>(type: "TEXT", nullable: false),
                Units = table.Column<int>(type: "INTEGER", nullable: false),
                RequestId = table.Column<string>(type: "TEXT", nullable: true),
                InputTokens = table.Column<long>(type: "INTEGER", nullable: true),
                OutputTokens = table.Column<long>(type: "INTEGER", nullable: true),
                ResultJson = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiExecutions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Competitors",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                AliasesJson = table.Column<string>(type: "TEXT", nullable: false),
                ProfileJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Competitors", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Jobs",
            columns: table => new
            {
                TrackingID = table.Column<Guid>(type: "TEXT", nullable: false),
                QueueID = table.Column<string>(type: "TEXT", nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                ExecuteAfter = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpireOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                DequeueAfter = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false),
                Error = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Jobs", x => x.TrackingID);
            });

        migrationBuilder.CreateTable(
            name: "Publishers",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Website = table.Column<string>(type: "TEXT", nullable: false),
                TopicsJson = table.Column<string>(type: "TEXT", nullable: false),
                ProfileJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Publishers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Runs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceId = table.Column<string>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                FinishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Discovered = table.Column<int>(type: "INTEGER", nullable: false),
                Published = table.Column<int>(type: "INTEGER", nullable: false),
                Reviewed = table.Column<int>(type: "INTEGER", nullable: false),
                Error = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Runs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Activities",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CompetitorId = table.Column<string>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Activities", x => x.Id);
                table.ForeignKey(
                    name: "FK_Activities_Competitors_CompetitorId",
                    column: x => x.CompetitorId,
                    principalTable: "Competitors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Sources",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                PublisherId = table.Column<string>(type: "TEXT", nullable: false),
                Url = table.Column<string>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", nullable: false),
                Approved = table.Column<bool>(type: "INTEGER", nullable: false),
                AllowArticleCapture = table.Column<bool>(type: "INTEGER", nullable: false),
                RightsAcknowledgement = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                NextDueUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastSuccessUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ETag = table.Column<string>(type: "TEXT", nullable: true),
                LastModified = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sources", x => x.Id);
                table.ForeignKey(
                    name: "FK_Sources_Publishers_PublisherId",
                    column: x => x.PublisherId,
                    principalTable: "Publishers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Documents",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                SourceId = table.Column<string>(type: "TEXT", nullable: false),
                CanonicalUrl = table.Column<string>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", nullable: false),
                PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                DatePrecision = table.Column<string>(type: "TEXT", nullable: false),
                FetchedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ContentHash = table.Column<string>(type: "TEXT", nullable: false),
                ParagraphsJson = table.Column<string>(type: "TEXT", nullable: false),
                Availability = table.Column<string>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Documents", x => x.Id);
                table.ForeignKey(
                    name: "FK_Documents_Sources_SourceId",
                    column: x => x.SourceId,
                    principalTable: "Sources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SearchHits",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                SourceId = table.Column<string>(type: "TEXT", nullable: false),
                Url = table.Column<string>(type: "TEXT", nullable: false),
                RequestId = table.Column<string>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", nullable: false),
                Snippet = table.Column<string>(type: "TEXT", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: true),
                DiscoveredUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SearchHits", x => x.Id);
                table.ForeignKey(
                    name: "FK_SearchHits_Sources_SourceId",
                    column: x => x.SourceId,
                    principalTable: "Sources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Facts",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CompetitorId = table.Column<string>(type: "TEXT", nullable: false),
                DocumentId = table.Column<string>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", nullable: false),
                Label = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false),
                Currency = table.Column<string>(type: "TEXT", nullable: true),
                Period = table.Column<string>(type: "TEXT", nullable: true),
                EvidenceJson = table.Column<string>(type: "TEXT", nullable: false),
                Approved = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Facts", x => x.Id);
                table.ForeignKey(
                    name: "FK_Facts_Competitors_CompetitorId",
                    column: x => x.CompetitorId,
                    principalTable: "Competitors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Facts_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Reviews",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                DocumentId = table.Column<string>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false),
                ProposalJson = table.Column<string>(type: "TEXT", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                DecidedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                DecidedBy = table.Column<string>(type: "TEXT", nullable: true),
                DecisionNote = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_Reviews_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Signals",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                CompetitorId = table.Column<string>(type: "TEXT", nullable: false),
                PublisherId = table.Column<string>(type: "TEXT", nullable: false),
                DocumentId = table.Column<string>(type: "TEXT", nullable: true),
                PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Importance = table.Column<int>(type: "INTEGER", nullable: false),
                Category = table.Column<string>(type: "TEXT", nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Signals", x => x.Id);
                table.CheckConstraint("CK_Importance", "\"Importance\" BETWEEN 0 AND 100");
                table.ForeignKey(
                    name: "FK_Signals_Competitors_CompetitorId",
                    column: x => x.CompetitorId,
                    principalTable: "Competitors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Signals_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Signals_Publishers_PublisherId",
                    column: x => x.PublisherId,
                    principalTable: "Publishers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Activities_CompetitorId",
            table: "Activities",
            column: "CompetitorId");

        migrationBuilder.CreateIndex(
            name: "IX_AiExecutions_Provider_Operation_InputHash_CreatedUtc",
            table: "AiExecutions",
            columns: new[] { "Provider", "Operation", "InputHash", "CreatedUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Documents_CanonicalUrl_ContentHash",
            table: "Documents",
            columns: new[] { "CanonicalUrl", "ContentHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Documents_SourceId",
            table: "Documents",
            column: "SourceId");

        migrationBuilder.CreateIndex(
            name: "IX_Facts_CompetitorId_DocumentId_Kind_Label",
            table: "Facts",
            columns: new[] { "CompetitorId", "DocumentId", "Kind", "Label" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Facts_DocumentId",
            table: "Facts",
            column: "DocumentId");

        migrationBuilder.CreateIndex(
            name: "IX_Jobs_QueueID_IsComplete_ExecuteAfter",
            table: "Jobs",
            columns: new[] { "QueueID", "IsComplete", "ExecuteAfter" });

        migrationBuilder.CreateIndex(
            name: "IX_Reviews_DocumentId",
            table: "Reviews",
            column: "DocumentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SearchHits_SourceId",
            table: "SearchHits",
            column: "SourceId");

        migrationBuilder.CreateIndex(
            name: "IX_Signals_CompetitorId",
            table: "Signals",
            column: "CompetitorId");

        migrationBuilder.CreateIndex(
            name: "IX_Signals_DocumentId",
            table: "Signals",
            column: "DocumentId");

        migrationBuilder.CreateIndex(
            name: "IX_Signals_PublishedUtc_Id",
            table: "Signals",
            columns: new[] { "PublishedUtc", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_Signals_PublisherId",
            table: "Signals",
            column: "PublisherId");

        migrationBuilder.CreateIndex(
            name: "IX_Sources_PublisherId",
            table: "Sources",
            column: "PublisherId");

        migrationBuilder.CreateIndex(
            name: "IX_Sources_Url",
            table: "Sources",
            column: "Url",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Activities");

        migrationBuilder.DropTable(
            name: "AiExecutions");

        migrationBuilder.DropTable(
            name: "Facts");

        migrationBuilder.DropTable(
            name: "Jobs");

        migrationBuilder.DropTable(
            name: "Reviews");

        migrationBuilder.DropTable(
            name: "Runs");

        migrationBuilder.DropTable(
            name: "SearchHits");

        migrationBuilder.DropTable(
            name: "Signals");

        migrationBuilder.DropTable(
            name: "Competitors");

        migrationBuilder.DropTable(
            name: "Documents");

        migrationBuilder.DropTable(
            name: "Sources");

        migrationBuilder.DropTable(
            name: "Publishers");
    }
}
