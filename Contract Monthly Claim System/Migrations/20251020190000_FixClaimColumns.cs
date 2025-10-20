using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contract_Monthly_Claim_System.Migrations
{
    public partial class FixClaimColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new nullable LecturerUserId string column (will match your model's type)
            migrationBuilder.AddColumn<string>(
                name: "LecturerUserId",
                table: "Claims",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            // Rename existing FilePath -> DocumentPath (preserves existing data)
            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "Claims",
                newName: "DocumentPath");

            // Create index + FK to AspNetUsers for LecturerUserId (nullable for safety)
            migrationBuilder.CreateIndex(
                name: "IX_Claims_LecturerUserId",
                table: "Claims",
                column: "LecturerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_AspNetUsers_LecturerUserId",
                table: "Claims",
                column: "LecturerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Claims_AspNetUsers_LecturerUserId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_LecturerUserId",
                table: "Claims");

            migrationBuilder.RenameColumn(
                name: "DocumentPath",
                table: "Claims",
                newName: "FilePath");

            migrationBuilder.DropColumn(
                name: "LecturerUserId",
                table: "Claims");
        }
    }
}