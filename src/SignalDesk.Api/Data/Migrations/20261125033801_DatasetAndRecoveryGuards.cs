using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalDesk.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261125033801_DatasetAndRecoveryGuards : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastCheckedUtc",
            table: "SearchHits",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "JobTrackingId",
            table: "Runs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Dataset",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Mode = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Dataset", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Dataset");

        migrationBuilder.DropColumn(
            name: "LastCheckedUtc",
            table: "SearchHits");

        migrationBuilder.DropColumn(
            name: "JobTrackingId",
            table: "Runs");
    }
}
