using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kavita.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTtsConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ApiKeyEncrypted = table.Column<byte[]>(type: "BLOB", nullable: true),
                    DefaultModel = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false, defaultValue: "tts-1"),
                    DefaultVoice = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false, defaultValue: "alloy"),
                    DefaultSpeed = table.Column<float>(type: "REAL", nullable: false, defaultValue: 1.0f),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTtsConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTtsConfigs_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<bool>(
                name: "TtsEnabled",
                table: "AppUserReadingProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TtsVoiceOverride",
                table: "AppUserReadingProfiles",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "TtsSpeedOverride",
                table: "AppUserReadingProfiles",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTtsConfigs_AppUserId",
                table: "UserTtsConfigs",
                column: "AppUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TtsEnabled", table: "AppUserReadingProfiles");
            migrationBuilder.DropColumn(name: "TtsVoiceOverride", table: "AppUserReadingProfiles");
            migrationBuilder.DropColumn(name: "TtsSpeedOverride", table: "AppUserReadingProfiles");

            migrationBuilder.DropTable(
                name: "UserTtsConfigs");
        }
    }
}
