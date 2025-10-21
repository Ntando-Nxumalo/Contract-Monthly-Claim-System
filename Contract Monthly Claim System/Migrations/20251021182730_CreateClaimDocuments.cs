using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contract_Monthly_Claim_System.Migrations
{
    /// <inheritdoc />
    public partial class CreateClaimDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Lecturers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "LecturerId",
                table: "Claims");

            // Conditional rename: only rename if FilePath exists and DocumentPath does NOT exist.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Claims','FilePath') IS NOT NULL AND COL_LENGTH('dbo.Claims','DocumentPath') IS NULL
BEGIN
    EXEC sp_rename N'[Claims].[FilePath]', N'DocumentPath', N'COLUMN';
END
");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Claims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Claims",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            // Add LecturerUserId only if it does not already exist
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Claims','LecturerUserId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Claims] ADD [LecturerUserId] nvarchar(450) NOT NULL DEFAULT N'';
END
");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "ClaimDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimDocuments_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create IX_Claims_LecturerUserId only if it doesn't exist
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Claims_LecturerUserId' AND object_id = OBJECT_ID('dbo.Claims'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Claims_LecturerUserId] ON [dbo].[Claims]([LecturerUserId]);
END
");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDocuments_ClaimId",
                table: "ClaimDocuments",
                column: "ClaimId");

            // Add FK only if it doesn't already exist
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Claims_AspNetUsers_LecturerUserId' AND parent_object_id = OBJECT_ID('dbo.Claims'))
BEGIN
    ALTER TABLE [dbo].[Claims] WITH CHECK ADD CONSTRAINT [FK_Claims_AspNetUsers_LecturerUserId] FOREIGN KEY([LecturerUserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE;
    ALTER TABLE [dbo].[Claims] CHECK CONSTRAINT [FK_Claims_AspNetUsers_LecturerUserId];
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FK if exists
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Claims_AspNetUsers_LecturerUserId' AND parent_object_id = OBJECT_ID('dbo.Claims'))
BEGIN
    ALTER TABLE [dbo].[Claims] DROP CONSTRAINT [FK_Claims_AspNetUsers_LecturerUserId];
END
");

            migrationBuilder.DropTable(
                name: "ClaimDocuments");

            // Drop index if exists
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Claims_LecturerUserId' AND object_id = OBJECT_ID('dbo.Claims'))
BEGIN
    DROP INDEX [IX_Claims_LecturerUserId] ON [dbo].[Claims];
END
");

            // Drop column only if it exists
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Claims','LecturerUserId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Claims] DROP COLUMN [LecturerUserId];
END
");

            // Conditional rename back: only rename if DocumentPath exists and FilePath does NOT exist.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Claims','DocumentPath') IS NOT NULL AND COL_LENGTH('dbo.Claims','FilePath') IS NULL
BEGIN
    EXEC sp_rename N'[Claims].[DocumentPath]', N'FilePath', N'COLUMN';
END
");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Claims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Claims",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<int>(
                name: "LecturerId",
                table: "Claims",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.InsertData(
                table: "Lecturers",
                columns: new[] { "Id", "Email", "Name", "Role" },
                values: new object[] { 1, "sarah@uni.edu", "Dr. Sarah Johnson", "Lecturer" });
        }
    }
}
