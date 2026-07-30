using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWageSettingsAndOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create WageSettings Table if it doesn't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[WageSettings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [WageSettings] (
                        [Id] uniqueidentifier NOT NULL,
                        [CptDefaultPayFrequency] int NOT NULL DEFAULT 0,
                        [JhbDefaultPayFrequency] int NOT NULL DEFAULT 1,
                        [WeeklyShiftCutoffDay] int NOT NULL DEFAULT 3,
                        [BibcRatePerDay] decimal(18,2) NOT NULL DEFAULT 28.75,
                        [DefaultSupervisorFee] decimal(18,2) NOT NULL DEFAULT 500.00,
                        [DefaultCompanyHousingWashingFee] decimal(18,2) NOT NULL DEFAULT 0.00,
                        [DefaultShiftStartTime] time NOT NULL DEFAULT '07:00:00',
                        [DefaultShiftEndTime] time NOT NULL DEFAULT '17:00:00',
                        [LunchEndHourThreshold] int NOT NULL DEFAULT 13,
                        [DeductLunchOnSaturday] bit NOT NULL DEFAULT 0,
                        [DeductLunchOnSunday] bit NOT NULL DEFAULT 0,
                        [DeductLunchOnPublicHoliday] bit NOT NULL DEFAULT 0,
                        [EnableProjectedHours] bit NOT NULL DEFAULT 1,
                        [AutoRecoverAdHocAdvances] bit NOT NULL DEFAULT 1,
                        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] datetime2 NULL,
                        [IsDeleted] bit NOT NULL DEFAULT 0,
                        CONSTRAINT [PK_WageSettings] PRIMARY KEY ([Id])
                    );
                END
            ");

            // 2. Add RunType and PayFrequency columns to WageRuns table safely
            migrationBuilder.Sql("IF COL_LENGTH('WageRuns', 'RunType') IS NULL ALTER TABLE [WageRuns] ADD [RunType] int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("IF COL_LENGTH('WageRuns', 'PayFrequency') IS NULL ALTER TABLE [WageRuns] ADD [PayFrequency] int NOT NULL DEFAULT 1;");

            // 3. Add DeductionAdvanceRecovery column to WageRunLines table safely
            migrationBuilder.Sql("IF COL_LENGTH('WageRunLines', 'DeductionAdvanceRecovery') IS NULL ALTER TABLE [WageRunLines] ADD [DeductionAdvanceRecovery] decimal(18,2) NOT NULL DEFAULT 0.0;");

            // 4. Data Migration & Backfill SQL Execution
            migrationBuilder.Sql(@"
                -- Backfill PayFrequency = Weekly (0) for Cape Town hourly runs
                UPDATE [WageRuns] 
                SET [PayFrequency] = 0 
                WHERE ([Branch] LIKE '%Cape%' OR [Branch] LIKE '%CPT%') 
                  AND ([PayType] IS NULL OR [PayType] <> 'MonthlySalary');

                -- Backfill PayFrequency = Fortnightly (1) for JHB / All hourly runs
                UPDATE [WageRuns] 
                SET [PayFrequency] = 1 
                WHERE ([Branch] IS NULL OR [Branch] LIKE '%Johannesburg%' OR [Branch] LIKE '%JHB%' OR [Branch] LIKE '%All%') 
                  AND ([PayType] IS NULL OR [PayType] <> 'MonthlySalary');

                -- Backfill PayFrequency = Monthly (2) for Monthly Salary runs
                UPDATE [WageRuns] 
                SET [PayFrequency] = 2 
                WHERE [PayType] = 'MonthlySalary';

                -- Backfill RunType = Standard (0) for all existing runs
                UPDATE [WageRuns] 
                SET [RunType] = 0 
                WHERE [RunType] IS NULL;

                -- Backfill PaidWageRunId on historical attendance records (UnpaidLeave, UnpaidSick, Absent) that occurred during finalized runs
                UPDATE ar
                SET ar.[PaidWageRunId] = wr.[Id]
                FROM [AttendanceRecords] ar
                CROSS APPLY (
                    SELECT TOP 1 w.[Id]
                    FROM [WageRuns] w
                    WHERE (w.[Status] = 1 OR w.[Status] = 2) -- Finalized or Paid
                      AND ar.[Date] >= w.[StartDate] 
                      AND ar.[Date] <= w.[EndDate]
                    ORDER BY w.[StartDate] DESC
                ) wr
                WHERE ar.[PaidWageRunId] IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH('WageRunLines', 'DeductionAdvanceRecovery') IS NOT NULL ALTER TABLE [WageRunLines] DROP COLUMN [DeductionAdvanceRecovery];");
            migrationBuilder.Sql("IF COL_LENGTH('WageRuns', 'PayFrequency') IS NOT NULL ALTER TABLE [WageRuns] DROP COLUMN [PayFrequency];");
            migrationBuilder.Sql("IF COL_LENGTH('WageRuns', 'RunType') IS NOT NULL ALTER TABLE [WageRuns] DROP COLUMN [RunType];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[WageSettings]', N'U') IS NOT NULL DROP TABLE [WageSettings];");
        }
    }
}
