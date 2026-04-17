using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteBaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "WageRuns",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "WageRuns",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [WageRuns] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "WageRuns",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "WageRunLines",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "WageRunLines",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [WageRunLines] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "WageRunLines",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "TaskComments",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Incidents",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Incidents",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [Incidents] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Incidents",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "IncidentPhotos",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "IncidentPhotos",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [IncidentPhotos] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "IncidentPhotos",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "HseqTrainingRecords",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "HseqTrainingRecords",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [HseqTrainingRecords] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "HseqTrainingRecords",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "HseqSafeHourRecords",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "HseqSafeHourRecords",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [HseqSafeHourRecords] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "HseqSafeHourRecords",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "HseqDocuments",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "HseqDocuments",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [HseqDocuments] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "HseqDocuments",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "HseqAuditSections",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "HseqAuditSections",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [HseqAuditSections] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "HseqAuditSections",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "HseqAudits",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "HseqAudits",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [HseqAudits] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "HseqAudits",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "HseqAuditNonComplianceItems",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "HseqAuditNonComplianceItems",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [HseqAuditNonComplianceItems] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "HseqAuditNonComplianceItems",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "HseqAuditComplianceItems",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "HseqAuditComplianceItems",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [HseqAuditComplianceItems] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "HseqAuditComplianceItems",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "BugComments",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "AttendanceRecords",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "AttendanceRecords",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE [AttendanceRecords] SET [IsActive] = CASE WHEN [IsActive] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "AttendanceRecords",
                newName: "CreatedAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WageRuns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WageRuns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WageRunLines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WageRunLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "TimeRecords",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TimeRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TimeRecords",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TimeRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "TimeRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Teams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Teams",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Teams",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "TeamMembers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TeamMembers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TeamMembers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TeamMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "TeamMembers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TaskComments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TaskComments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TaskComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "TaskComments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "TaskAttachments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TaskAttachments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TaskAttachments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TaskAttachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "TaskAttachments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "TaskAssignments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TaskAssignments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TaskAssignments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TaskAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "TaskAssignments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Suppliers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "PublicHolidays",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PublicHolidays",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PublicHolidays",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "PublicHolidays",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "PublicHolidays",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "ProjectVariationOrders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProjectVariationOrders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ProjectVariationOrders",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "ProjectVariationOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ProjectVariationOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "ProjectTasks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProjectTasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ProjectTasks",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "ProjectTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ProjectTasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Projects",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "OvertimeRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OvertimeRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "OvertimeRequests",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "OvertimeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "OvertimeRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "OrderLines",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "OrderLines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "OrderLines",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "OrderLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "OrderLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Notifications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "NotificationDismissals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "NotificationDismissals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "NotificationDismissals",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "NotificationDismissals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "NotificationDismissals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LeaveRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "LeaveRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "InventoryItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "InventoryItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "InventoryItems",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "InventoryItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "InventoryItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Incidents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Incidents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "IncidentPhotos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "IncidentPhotos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqTrainingRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqTrainingRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqSafeHourRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqSafeHourRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqAuditSections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqAuditSections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqAudits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqAudits",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqAuditNonComplianceItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqAuditNonComplianceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqAuditComplianceItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqAuditComplianceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "HseqAuditAttachments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "HseqAuditAttachments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "HseqAuditAttachments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "HseqAuditAttachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "HseqAuditAttachments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Employees",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Customers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "CustomerContacts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CustomerContacts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CustomerContacts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "CustomerContacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "CustomerContacts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "BugReports",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BugReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BugReports",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "BugReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BugReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BugComments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BugComments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "BugComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BugComments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AttendanceRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "AttendanceRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AppSettings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "AppSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6500), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6486), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6489), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("496a7469-aa27-435d-899c-1a7c540f5187"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6504), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6501), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6497), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6496), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6484), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6502), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6154), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6499), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6487), "System", true, null, null });

            migrationBuilder.UpdateData(
                table: "PublicHolidays",
                keyColumn: "Id",
                keyValue: new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"),
                columns: new[] { "CreatedAtUtc", "CreatedBy", "IsActive", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 2, 3, 18, 17, 59, 86, DateTimeKind.Utc).AddTicks(6505), "System", true, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WageRuns");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WageRuns");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WageRunLines");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WageRunLines");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "TimeRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TimeRecords");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TimeRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TimeRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "TimeRecords");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "NotificationDismissals");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "NotificationDismissals");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "NotificationDismissals");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "NotificationDismissals");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "NotificationDismissals");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "IncidentPhotos");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "IncidentPhotos");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqTrainingRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqTrainingRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqSafeHourRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqSafeHourRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqDocuments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqAuditSections");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqAuditSections");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqAudits");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqAudits");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqAuditNonComplianceItems");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqAuditNonComplianceItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqAuditComplianceItems");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqAuditComplianceItems");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "HseqAuditAttachments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "HseqAuditAttachments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "HseqAuditAttachments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "HseqAuditAttachments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HseqAuditAttachments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BugComments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BugComments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "BugComments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BugComments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AppSettings");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "WageRuns",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "WageRuns",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [WageRuns] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "WageRuns",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "WageRunLines",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "WageRunLines",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [WageRunLines] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "WageRunLines",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "TaskComments",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Incidents",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Incidents",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [Incidents] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Incidents",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "IncidentPhotos",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "IncidentPhotos",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [IncidentPhotos] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "IncidentPhotos",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "HseqTrainingRecords",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "HseqTrainingRecords",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [HseqTrainingRecords] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "HseqTrainingRecords",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "HseqSafeHourRecords",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "HseqSafeHourRecords",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [HseqSafeHourRecords] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "HseqSafeHourRecords",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "HseqDocuments",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "HseqDocuments",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [HseqDocuments] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "HseqDocuments",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "HseqAuditSections",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "HseqAuditSections",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [HseqAuditSections] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "HseqAuditSections",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "HseqAudits",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "HseqAudits",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [HseqAudits] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "HseqAudits",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "HseqAuditNonComplianceItems",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "HseqAuditNonComplianceItems",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [HseqAuditNonComplianceItems] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "HseqAuditNonComplianceItems",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "HseqAuditComplianceItems",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "HseqAuditComplianceItems",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [HseqAuditComplianceItems] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "HseqAuditComplianceItems",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "BugComments",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "AttendanceRecords",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "AttendanceRecords",
                newName: "IsDeleted");

            migrationBuilder.Sql("UPDATE [AttendanceRecords] SET [IsDeleted] = CASE WHEN [IsDeleted] = 1 THEN 0 ELSE 1 END");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "AttendanceRecords",
                newName: "CreatedAt");
        }
    }
}
