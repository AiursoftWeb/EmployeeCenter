using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCollectionChannelAmountToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE CollectionRecords SET ExpectedAmount = ExpectedAmount * 100;");
            migrationBuilder.Sql("UPDATE CollectionRecords SET ActualAmount = ActualAmount * 100;");
            migrationBuilder.Sql("UPDATE CollectionChannels SET ReferenceAmount = ReferenceAmount * 100;");

            migrationBuilder.AlterColumn<long>(
                name: "ExpectedAmount",
                table: "CollectionRecords",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<long>(
                name: "ActualAmount",
                table: "CollectionRecords",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<long>(
                name: "ReferenceAmount",
                table: "CollectionChannels",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedAmount",
                table: "CollectionRecords",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualAmount",
                table: "CollectionRecords",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReferenceAmount",
                table: "CollectionChannels",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
