using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalDesk.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261126173632_AddRunProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Processed",
            table: "Runs",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Step",
            table: "Runs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Total",
            table: "Runs",
            type: "INTEGER",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Processed",
            table: "Runs");

        migrationBuilder.DropColumn(
            name: "Step",
            table: "Runs");

        migrationBuilder.DropColumn(
            name: "Total",
            table: "Runs");
    }
}
