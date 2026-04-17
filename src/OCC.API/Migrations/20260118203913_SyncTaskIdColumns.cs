using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCC.API.Migrations
{
    /// <inheritdoc />
    public partial class SyncTaskIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- 1. Ensure TaskId exists in TaskComments
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaskComments]') AND name = N'TaskId')
                BEGIN
                    ALTER TABLE [TaskComments] ADD [TaskId] uniqueidentifier NULL;
                END

                -- 2. Ensure TaskId exists in TaskAssignments
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaskAssignments]') AND name = N'TaskId')
                BEGIN
                    ALTER TABLE [TaskAssignments] ADD [TaskId] uniqueidentifier NULL;
                END

                -- 3. If ProjectTaskId still exists in TaskAssignments, copy it to TaskId
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaskAssignments]') AND name = N'ProjectTaskId')
                BEGIN
                    EXEC('UPDATE [TaskAssignments] SET [TaskId] = [ProjectTaskId] WHERE [TaskId] IS NULL');
                END
                
                -- Note: TaskComments' ProjectTaskId was already dropped in the previous migration, 
                -- so we can't recover that one easily if TaskId was also missing.
            ");

            // Also ensure the foreign key constraints exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TaskComments_ProjectTasks_TaskId')
                BEGIN
                    ALTER TABLE [TaskComments] ADD CONSTRAINT [FK_TaskComments_ProjectTasks_TaskId] 
                    FOREIGN KEY ([TaskId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE CASCADE;
                END

                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TaskAssignments_ProjectTasks_TaskId')
                BEGIN
                    ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_ProjectTasks_TaskId] 
                    FOREIGN KEY ([TaskId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE NO ACTION;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
