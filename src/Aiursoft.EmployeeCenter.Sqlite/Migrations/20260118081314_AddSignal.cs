using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.EmployeeCenter.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignalQuestionnaires",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalQuestionnaires", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignalQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Meta = table.Column<string>(type: "TEXT", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignalResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestionnaireId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    SubmitTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalResponses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignalResponses_SignalQuestionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "SignalQuestionnaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignalQuestionnaireQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestionnaireId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalQuestionnaireQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalQuestionnaireQuestions_SignalQuestionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "SignalQuestionnaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignalQuestionnaireQuestions_SignalQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "SignalQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignalQuestionResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SignalResponseId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalQuestionResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalQuestionResponses_SignalQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "SignalQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignalQuestionResponses_SignalResponses_SignalResponseId",
                        column: x => x.SignalResponseId,
                        principalTable: "SignalResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalQuestionnaireQuestions_QuestionId",
                table: "SignalQuestionnaireQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalQuestionnaireQuestions_QuestionnaireId",
                table: "SignalQuestionnaireQuestions",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalQuestionResponses_QuestionId",
                table: "SignalQuestionResponses",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalQuestionResponses_SignalResponseId",
                table: "SignalQuestionResponses",
                column: "SignalResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalResponses_QuestionnaireId",
                table: "SignalResponses",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalResponses_UserId",
                table: "SignalResponses",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalQuestionnaireQuestions");

            migrationBuilder.DropTable(
                name: "SignalQuestionResponses");

            migrationBuilder.DropTable(
                name: "SignalQuestions");

            migrationBuilder.DropTable(
                name: "SignalResponses");

            migrationBuilder.DropTable(
                name: "SignalQuestionnaires");
        }
    }
}
