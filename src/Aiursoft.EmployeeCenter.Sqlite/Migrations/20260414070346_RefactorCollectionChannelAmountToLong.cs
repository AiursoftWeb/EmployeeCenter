using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCollectionChannelAmountToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE CollectionRecords SET ExpectedAmount = CAST(CAST(ExpectedAmount AS REAL) * 100 AS INTEGER);");
            migrationBuilder.Sql("UPDATE CollectionRecords SET ActualAmount = CAST(CAST(ActualAmount AS REAL) * 100 AS INTEGER);");
            migrationBuilder.Sql("UPDATE CollectionChannels SET ReferenceAmount = CAST(CAST(ReferenceAmount AS REAL) * 100 AS INTEGER);");

            migrationBuilder.AlterColumn<long>(
                name: "ExpectedAmount",
                table: "CollectionRecords",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "ActualAmount",
                table: "CollectionRecords",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "ReferenceAmount",
                table: "CollectionChannels",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedAmount",
                table: "CollectionRecords",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualAmount",
                table: "CollectionRecords",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReferenceAmount",
                table: "CollectionChannels",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");
        }
    }
}
