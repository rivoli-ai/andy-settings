using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andy.Settings.Infrastructure.Migrations;

[DbContext(typeof(SettingsDbContext))]
[Migration("20260720233000_EnforceDeterministicIdentities")]
public partial class EnforceDeterministicIdentities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SettingDefinitions_Key_ApplicationCode",
            table: "SettingDefinitions");
        migrationBuilder.DropIndex(
            name: "IX_SettingAssignments_DefinitionId_ScopeType_ScopeId",
            table: "SettingAssignments");
        migrationBuilder.DropIndex(
            name: "IX_EncryptedSecrets_DefinitionId_ScopeType_ScopeId",
            table: "EncryptedSecrets");

        migrationBuilder.AddColumn<string>(
            name: "ScopeKey", table: "SettingAssignments", type: "character varying(256)",
            maxLength: 256, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(
            name: "ScopeKey", table: "EncryptedSecrets", type: "character varying(256)",
            maxLength: 256, nullable: false, defaultValue: "");

        migrationBuilder.Sql("UPDATE \"SettingAssignments\" SET \"ScopeKey\" = COALESCE(\"ScopeId\", '')");
        migrationBuilder.Sql("UPDATE \"EncryptedSecrets\" SET \"ScopeKey\" = COALESCE(\"ScopeId\", '')");
        // Remove any legacy plaintext that crossed the secret boundary before
        // writes were rejected, then normalize secret metadata/defaults.
        migrationBuilder.Sql(
            "DELETE FROM \"SettingAssignments\" WHERE \"DefinitionId\" IN " +
            "(SELECT \"Id\" FROM \"SettingDefinitions\" WHERE \"IsSecret\" = TRUE OR \"DataType\" = 'Secret')");
        migrationBuilder.Sql(
            "UPDATE \"SettingDefinitions\" SET \"IsSecret\" = TRUE, \"DefaultValueJson\" = NULL " +
            "WHERE \"IsSecret\" = TRUE OR \"DataType\" = 'Secret'");

        migrationBuilder.CreateIndex(
            name: "IX_SettingDefinitions_Key", table: "SettingDefinitions", column: "Key", unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_SettingAssignments_DefinitionId_ScopeType_ScopeKey",
            table: "SettingAssignments", columns: new[] { "DefinitionId", "ScopeType", "ScopeKey" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_EncryptedSecrets_DefinitionId_ScopeType_ScopeKey",
            table: "EncryptedSecrets", columns: new[] { "DefinitionId", "ScopeType", "ScopeKey" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SettingDefinitions_Key", table: "SettingDefinitions");
        migrationBuilder.DropIndex(
            name: "IX_SettingAssignments_DefinitionId_ScopeType_ScopeKey", table: "SettingAssignments");
        migrationBuilder.DropIndex(
            name: "IX_EncryptedSecrets_DefinitionId_ScopeType_ScopeKey", table: "EncryptedSecrets");
        migrationBuilder.DropColumn(name: "ScopeKey", table: "SettingAssignments");
        migrationBuilder.DropColumn(name: "ScopeKey", table: "EncryptedSecrets");
        migrationBuilder.CreateIndex(
            name: "IX_SettingDefinitions_Key_ApplicationCode", table: "SettingDefinitions",
            columns: new[] { "Key", "ApplicationCode" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_SettingAssignments_DefinitionId_ScopeType_ScopeId", table: "SettingAssignments",
            columns: new[] { "DefinitionId", "ScopeType", "ScopeId" });
        migrationBuilder.CreateIndex(
            name: "IX_EncryptedSecrets_DefinitionId_ScopeType_ScopeId", table: "EncryptedSecrets",
            columns: new[] { "DefinitionId", "ScopeType", "ScopeId" }, unique: true);
    }
}
