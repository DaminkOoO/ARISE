using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreationDesQuetes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hunter_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quest_date = table.Column<DateOnly>(type: "date", nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stat_target = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xp_reward = table.Column<int>(type: "integer", nullable: false),
                    is_fallback = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quests", x => x.id);
                    table.ForeignKey(
                        name: "fk_quests_hunter_profiles_hunter_profile_id",
                        column: x => x.hunter_profile_id,
                        principalTable: "hunter_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quests_hunter_profile_id_domain_quest_date",
                table: "quests",
                columns: new[] { "hunter_profile_id", "domain", "quest_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quests");
        }
    }
}
