using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalDesk.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261125034256_AiReconciliationAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ResolutionNote",
            table: "AiExecutions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ResolvedBy",
            table: "AiExecutions",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ResolutionNote",
            table: "AiExecutions");

        migrationBuilder.DropColumn(
            name: "ResolvedBy",
            table: "AiExecutions");
    }
}
