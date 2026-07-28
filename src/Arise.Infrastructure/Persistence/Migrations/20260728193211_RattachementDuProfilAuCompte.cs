using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RattachementDuProfilAuCompte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "hunter_profile_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_hunter_profile_id",
                table: "users",
                column: "hunter_profile_id",
                unique: true,
                filter: "hunter_profile_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_hunter_profile_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "hunter_profile_id",
                table: "users");
        }
    }
}
