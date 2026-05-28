using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.MySql.Migrations
{
    /// <inheritdoc />
    public partial class FixAssetRowVersionConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Assets",
                type: "longblob",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp(6)",
                oldRowVersion: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "RowVersion",
                table: "Assets",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "longblob");
        }
    }
}
