using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andy.Settings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefinitionKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ActorType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SettingDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ApplicationCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DataType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultValueJson = table.Column<string>(type: "text", nullable: true),
                    ValidationJson = table.Column<string>(type: "text", nullable: true),
                    UiSchemaJson = table.Column<string>(type: "text", nullable: true),
                    IsSecret = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedScopesJson = table.Column<string>(type: "text", nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncryptedSecrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EncryptedValue = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncryptedSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncryptedSecrets_SettingDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "SettingDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettingAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ValueJson = table.Column<string>(type: "text", nullable: false),
                    Etag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettingAssignments_SettingDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "SettingDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CreatedAt",
                table: "AuditEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_DefinitionKey",
                table: "AuditEvents",
                column: "DefinitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedSecrets_DefinitionId_ScopeType_ScopeId",
                table: "EncryptedSecrets",
                columns: new[] { "DefinitionId", "ScopeType", "ScopeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettingAssignments_DefinitionId_ScopeType_ScopeId",
                table: "SettingAssignments",
                columns: new[] { "DefinitionId", "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_SettingAssignments_ScopeType",
                table: "SettingAssignments",
                column: "ScopeType");

            migrationBuilder.CreateIndex(
                name: "IX_SettingDefinitions_ApplicationCode",
                table: "SettingDefinitions",
                column: "ApplicationCode");

            migrationBuilder.CreateIndex(
                name: "IX_SettingDefinitions_Category",
                table: "SettingDefinitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SettingDefinitions_Key_ApplicationCode",
                table: "SettingDefinitions",
                columns: new[] { "Key", "ApplicationCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "EncryptedSecrets");

            migrationBuilder.DropTable(
                name: "SettingAssignments");

            migrationBuilder.DropTable(
                name: "SettingDefinitions");
        }
    }
}
