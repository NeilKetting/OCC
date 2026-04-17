using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using OCC.Shared.Models;
using System;
using System.Linq;

namespace OCC.Client.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
            // Database.EnsureCreated();
            // SeedData();
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // Database.EnsureCreated();
            // SeedData();
        }

        private void SeedData()
        {
            if (!Users.Any())
            {
                Users.Add(new User
                {
                    Email = "neil@mdk.co.za",
                    Password = "pass",
                    FirstName = "Neil",
                    LastName = "Ketting",
                    UserRole = UserRole.Admin,
                    Location = "South Africa",
                    IsApproved = true,
                    IsEmailVerified = true
                });

                SaveChanges();

                Users.Add(new User
                {
                    Email = "vernon@orange-circle.co.za",
                    Password = "Password",
                    FirstName = "Vernon",
                    LastName = "Steenberg",
                    UserRole = UserRole.Admin,
                    Location = "South Africa",
                    IsApproved = true,
                    IsEmailVerified = true
                });

                SaveChanges();
            }
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectTask> ProjectTasks { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<TimeRecord> TimeRecords { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<ClockingEvent> ClockingEvents { get; set; }
        public DbSet<DailyTimesheet> DailyTimesheets { get; set; }
        
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }

        public DbSet<TaskAssignment> TaskAssignments { get; set; }

        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatSessionUser> ChatSessionUsers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatMessageAttachment> ChatMessageAttachments { get; set; }
        public DbSet<ChatMessageReadReceipt> ChatMessageReadReceipts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=OCC_Rev5_DB;Trusted_Connection=True;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Project -> Tasks (One-to-Many)
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting Project deletes Tasks

            // Project -> Customer (Optional One-to-Many or Many-to-One depending on need, assuming Many Projects -> One Customer)
            modelBuilder.Entity<Project>()
                .HasOne(p => p.CustomerEntity)
                .WithMany() // Assuming Customer doesn't need a Projects collection yet
                .OnDelete(DeleteBehavior.SetNull);

            // ProjectTask -> Comments (One-to-Many)
            modelBuilder.Entity<ProjectTask>()
                .HasMany(t => t.Comments)
                .WithOne(c => c.ProjectTask)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskAssignment -> ProjectTask (Many-to-One)
            modelBuilder.Entity<TaskAssignment>()
                .HasOne(ta => ta.ProjectTask)
                .WithMany(t => t.Assignments)
                .HasForeignKey(ta => ta.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TimeSpan to store as Ticks (bigint) to avoid overflow for >24h durations
            var timeSpanConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TimeSpan, long>(
                v => v.Ticks,
                v => TimeSpan.FromTicks(v));

            modelBuilder.Entity<ProjectTask>()
                .Property(nameof(ProjectTask.PlannedDurationHours))
                .HasConversion(timeSpanConverter);

            modelBuilder.Entity<ProjectTask>()
                .Property(nameof(ProjectTask.ActualDuration))
                .HasConversion(timeSpanConverter);
        }
    }
}
