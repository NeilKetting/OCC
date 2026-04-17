using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectTaskIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Safely drop ProjectTaskId from TaskAssignments if it exists
                IF EXISTS(SELECT * FROM sys.columns WHERE Name = N'ProjectTaskId' AND Object_ID = Object_ID(N'TaskAssignments'))
                BEGIN
                    -- 1. Drop Foreign Key Constraint if it exists
                    DECLARE @ConstraintName nvarchar(200)
                    SELECT TOP 1 @ConstraintName = fk.name 
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.columns c ON fkc.parent_column_id = c.column_id AND fkc.parent_object_id = c.object_id
                    WHERE fk.parent_object_id = Object_ID(N'TaskAssignments') 
                    AND c.name = N'ProjectTaskId'

                    IF @ConstraintName IS NOT NULL
                    BEGIN
                        EXEC('ALTER TABLE [TaskAssignments] DROP CONSTRAINT [' + @ConstraintName + ']')
                    END

                    -- 2. Drop Index if it exists
                    IF EXISTS(SELECT * FROM sys.indexes WHERE name = 'IX_TaskAssignments_ProjectTaskId' AND object_id = OBJECT_ID('TaskAssignments'))
                    BEGIN
                        DROP INDEX [IX_TaskAssignments_ProjectTaskId] ON [TaskAssignments]
                    END
                    
                    -- 3. Finally Drop the Column
                    ALTER TABLE [TaskAssignments] DROP COLUMN [ProjectTaskId]
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // We usually don't implement Down for destructive cleanup of legacy/ghost columns
            // as we can't restore the lost data easily, and it shouldn't be there anyway.
        }
    }
}
