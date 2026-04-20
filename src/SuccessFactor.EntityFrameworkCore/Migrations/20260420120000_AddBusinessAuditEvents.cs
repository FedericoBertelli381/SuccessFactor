using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SuccessFactor.EntityFrameworkCore;

#nullable disable

namespace SuccessFactor.Migrations
{
    [DbContext(typeof(SuccessFactorDbContext))]
    [Migration("20260420120000_AddBusinessAuditEvents")]
    public partial class AddBusinessAuditEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessAuditEvents",
                schema: "dbo",
                columns: table => new
                {
                    BusinessAuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditEvents", x => x.BusinessAuditEventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditEvents_TenantId_Action",
                schema: "dbo",
                table: "BusinessAuditEvents",
                columns: new[] { "TenantId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditEvents_TenantId_EntityType",
                schema: "dbo",
                table: "BusinessAuditEvents",
                columns: new[] { "TenantId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditEvents_TenantId_EventTime",
                schema: "dbo",
                table: "BusinessAuditEvents",
                columns: new[] { "TenantId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditEvents_TenantId_UserName",
                schema: "dbo",
                table: "BusinessAuditEvents",
                columns: new[] { "TenantId", "UserName" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessAuditEvents",
                schema: "dbo");
        }
    }
}
