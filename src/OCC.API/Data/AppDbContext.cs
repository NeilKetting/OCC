using Microsoft.EntityFrameworkCore;
using OCC.Shared.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace OCC.API.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor = null!) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Suppress pending changes warning to unblock migration application on remote server
            optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectTask> ProjectTasks { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerContact> CustomerContacts { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }
        public DbSet<SubContractor> SubContractors { get; set; }

        public DbSet<TimeRecord> TimeRecords { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<PublicHoliday> PublicHolidays { get; set; }
        public DbSet<OvertimeRequest> OvertimeRequests { get; set; }
        public DbSet<WageRun> WageRuns { get; set; }
        public DbSet<WageRunLine> WageRunLines { get; set; }
        public DbSet<EmployeeLoan> EmployeeLoans { get; set; }

        public DbSet<ClockingEvent> ClockingEvents { get; set; }
        public DbSet<DailyTimesheet> DailyTimesheets { get; set; }

        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<TaskAssignment> TaskAssignments { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<TaskAttachment> TaskAttachments { get; set; }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<BugReport> BugReports { get; set; }
        public DbSet<BugComment> BugComments { get; set; }

        // HSEQ Modules
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<IncidentPhoto> IncidentPhotos { get; set; }
        public DbSet<IncidentDocument> IncidentDocuments { get; set; }
        public DbSet<HseqAudit> HseqAudits { get; set; }
        public DbSet<HseqAuditSection> HseqAuditSections { get; set; }
        public DbSet<HseqAuditComplianceItem> HseqAuditComplianceItems { get; set; }
        public DbSet<HseqAuditNonComplianceItem> HseqAuditNonComplianceItems { get; set; }
        public DbSet<HseqTrainingRecord> HseqTrainingRecords { get; set; }
        public DbSet<HseqSafeHourRecord> HseqSafeHourRecords { get; set; }
        public DbSet<HseqDocument> HseqDocuments { get; set; }
        public DbSet<HseqAuditAttachment> HseqAuditAttachments { get; set; }
        
        public DbSet<NotificationDismissal> NotificationDismissals { get; set; }
        public DbSet<ProjectVariationOrder> ProjectVariationOrders { get; set; }
        public DbSet<LogUploadRequest> LogUploads { get; set; }

        // Chat System
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatSessionUser> ChatSessionUsers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatMessageAttachment> ChatMessageAttachments { get; set; }
        public DbSet<ChatMessageReadReceipt> ChatMessageReadReceipts { get; set; }
        public DbSet<SnagJob> SnagJobs { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry);
                auditEntry.TableName = entry.Entity.GetType().Name; 
                
                // Get User ID
                var userId = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
                auditEntry.UserId = userId ?? "System"; 

                auditEntries.Add(auditEntry);

                // BaseEntity / IAuditableEntity Logic
                if (entry.Entity is BaseEntity baseEntity)
                {
                    var now = DateTime.UtcNow;
                    var user = userId ?? "System";

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            baseEntity.CreatedAtUtc = now;
                            baseEntity.CreatedBy = user;
                            baseEntity.IsActive = true;
                            break;
                        
                        case EntityState.Modified:
                            baseEntity.UpdatedAtUtc = now;
                            baseEntity.UpdatedBy = user;
                            break;

                        case EntityState.Deleted:
                            // Soft Delete Logic
                            entry.State = EntityState.Modified;
                            baseEntity.IsActive = false;
                            baseEntity.UpdatedAtUtc = now;
                            baseEntity.UpdatedBy = user;
                            
                            // Adjust audit for soft delete
                            auditEntry.AuditType = "Delete"; 
                            // We need to capture old values for soft delete as if it was a delete?
                            // Technically it's an update IS_ACTIVE=false, but conceptually a delete.
                            // The original code set AuditType="Delete". 
                            // Let's copy properties manually since state changed to Modified.
                            foreach (var p in entry.Properties)
                            {
                                 auditEntry.OldValues[p.Metadata.Name] = p.OriginalValue;
                            }
                            break;
                    }
                }

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = "Create";
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.AuditType = "Delete";
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.ChangedColumns.Add(propertyName);
                                auditEntry.AuditType = "Update";
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            // Save audit entities that have all the data we need
            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return Task.CompletedTask;

            foreach (var auditEntry in auditEntries)
            {
                // Get the final value of the temporary properties
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }

                // Save the AuditLog
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            return base.SaveChangesAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships if needed
            // modelBuilder.Entity<ProjectTask>()
            //     .HasOne<Project>()
            //     .WithMany()
            //     .HasForeignKey(t => t.ProjectId);

            // Seed SA Public Holidays 2026
            modelBuilder.Entity<PublicHoliday>().HasData(
                new PublicHoliday { Id = new Guid("b862c2f5-9fe1-4228-9946-4d0aa0fdb12a"), Date = new DateTime(2026, 1, 1), Name = "New Year's Day" },
                new PublicHoliday { Id = new Guid("a1e140e8-e1a8-4acf-b5e0-715ed41c7af3"), Date = new DateTime(2026, 3, 21), Name = "Human Rights Day" },
                new PublicHoliday { Id = new Guid("2d50946b-c807-4e9f-a74d-a6c5493b3c94"), Date = new DateTime(2026, 4, 3), Name = "Good Friday" },
                new PublicHoliday { Id = new Guid("e91fa4f6-1b80-423b-8755-c8e133c34670"), Date = new DateTime(2026, 4, 6), Name = "Family Day" },
                new PublicHoliday { Id = new Guid("3e473dfe-4182-4c81-8ba8-f5c33a9e1ed1"), Date = new DateTime(2026, 4, 27), Name = "Freedom Day" },
                new PublicHoliday { Id = new Guid("80ce73e9-fd26-47db-b79f-57165ba68111"), Date = new DateTime(2026, 5, 1), Name = "Workers' Day" },
                new PublicHoliday { Id = new Guid("7f422560-941b-4fe4-80ef-b22adeddfbee"), Date = new DateTime(2026, 6, 16), Name = "Youth Day" },
                new PublicHoliday { Id = new Guid("e226a941-9246-4dd5-91ec-7dff8a5a96ca"), Date = new DateTime(2026, 8, 9), Name = "National Women's Day" },
                new PublicHoliday { Id = new Guid("0dc5e6d5-2530-40d7-8301-9d41f44c879b"), Date = new DateTime(2026, 8, 10), Name = "Public Holiday" }, // Observed
                new PublicHoliday { Id = new Guid("5eb30cce-ad23-43a9-9ca2-50236232dccf"), Date = new DateTime(2026, 9, 24), Name = "Heritage Day" },
                new PublicHoliday { Id = new Guid("b5b21171-4284-4f14-bfa4-e8bd0cdb3264"), Date = new DateTime(2026, 12, 16), Name = "Day of Reconciliation" },
                new PublicHoliday { Id = new Guid("496a7469-aa27-435d-899c-1a7c540f5187"), Date = new DateTime(2026, 12, 25), Name = "Christmas Day" },
                new PublicHoliday { Id = new Guid("fcc99eac-4678-49da-9e2e-f1026fe7c867"), Date = new DateTime(2026, 12, 26), Name = "Day of Goodwill" }
            );

            modelBuilder.Entity<OrderLine>(entity =>
            {
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                entity.Property(e => e.VatAmount).HasPrecision(18, 2);
                entity.Property(e => e.LineTotal).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasMany(e => e.Contacts)
                      .WithOne(e => e.Customer)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CustomerContact>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.TaxRate).HasPrecision(18, 4);
            });

            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.Property(e => e.AverageCost).HasPrecision(18, 2);
                entity.Property(e => e.Price).HasPrecision(18, 2);
            });

            modelBuilder.Entity<AttendanceRecord>(entity =>
            {
                entity.Property(e => e.CachedHourlyRate).HasPrecision(18, 2);
            });

            modelBuilder.Entity<DailyTimesheet>(entity =>
            {
                entity.Property(e => e.CalculatedHours).HasPrecision(18, 2);
                entity.Property(e => e.WageEstimated).HasPrecision(18, 2);
            });

            modelBuilder.Entity<EmployeeLoan>(entity =>
            {
                entity.Property(e => e.PrincipalAmount).HasPrecision(18, 2);
                entity.Property(e => e.MonthlyInstallment).HasPrecision(18, 2);
                entity.Property(e => e.OutstandingBalance).HasPrecision(18, 2);
                entity.Property(e => e.InterestRate).HasPrecision(18, 2);
            });

            modelBuilder.Entity<WageRunLine>(entity =>
            {
                entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
                entity.Property(e => e.TotalWage).HasPrecision(18, 2);
                entity.Property(e => e.DeductionLoan).HasPrecision(18, 2);
                entity.Property(e => e.DeductionTax).HasPrecision(18, 2);
                entity.Property(e => e.DeductionWashing).HasPrecision(18, 2);
                entity.Property(e => e.DeductionGas).HasPrecision(18, 2);
                entity.Property(e => e.DeductionOther).HasPrecision(18, 2);
                entity.Property(e => e.DeductionPPE).HasPrecision(18, 2);
                entity.Property(e => e.IncentiveSupervisor).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.TaxRate).HasPrecision(18, 2);
            });

            modelBuilder.Entity<OrderLine>(entity =>
            {
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                entity.Property(e => e.VatAmount).HasPrecision(18, 2);
                entity.Property(e => e.LineTotal).HasPrecision(18, 2);
            });

            modelBuilder.Entity<HseqAudit>(entity =>
            {
                entity.Property(e => e.TargetScore).HasPrecision(18, 2);
                entity.Property(e => e.ActualScore).HasPrecision(18, 2);
            });

            modelBuilder.Entity<HseqAuditSection>(entity =>
            {
                entity.Property(e => e.PossibleScore).HasPrecision(18, 2);
                entity.Property(e => e.ActualScore).HasPrecision(18, 2);
            });

            modelBuilder.Entity<ProjectVariationOrder>(entity =>
            {
                entity.HasOne(v => v.Project)
                    .WithMany(p => p.VariationOrders)
                    .HasForeignKey(v => v.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // HSEQ Configurations
            modelBuilder.Entity<HseqAudit>()
                .HasMany(a => a.Sections)
                .WithOne().HasForeignKey(s => s.AuditId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HseqAudit>()
                .HasMany(a => a.ComplianceItems)
                .WithOne().HasForeignKey(i => i.AuditId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HseqAudit>()
                .HasMany(a => a.NonComplianceItems)
                .WithOne().HasForeignKey(i => i.AuditId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HseqAudit>()
                .HasMany(a => a.Attachments)
                .WithOne(a => a.Audit).HasForeignKey(a => a.AuditId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HseqAuditNonComplianceItem>()
                .HasMany(i => i.Attachments)
                .WithOne(a => a.NonComplianceItem).HasForeignKey(a => a.NonComplianceItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HseqAudit>(entity =>
            {
                entity.Property(e => e.ActualScore).HasPrecision(18, 2);
                entity.Property(e => e.TargetScore).HasPrecision(18, 2);
            });

            modelBuilder.Entity<HseqAuditSection>(entity =>
            {
                entity.Property(e => e.ActualScore).HasPrecision(18, 2);
                entity.Property(e => e.PossibleScore).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Incident>()
                .HasMany(i => i.Photos)
                .WithOne().HasForeignKey(p => p.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<Incident>()
                .HasMany(i => i.Documents)
                .WithOne().HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectTask>(entity =>
            {
                entity.Property(e => e.PlannedDurationHours)
                    .HasConversion(
                        v => v.HasValue ? v.Value.Ticks : (long?)null,
                        v => v.HasValue ? TimeSpan.FromTicks(v.Value) : (TimeSpan?)null)
                    .HasColumnType("bigint");

                entity.HasOne(d => d.ParentTask)
                    .WithMany(p => p.Children)
                    .HasForeignKey(d => d.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.ActualDuration)
                    .HasConversion(
                        v => v.HasValue ? v.Value.Ticks : (long?)null,
                        v => v.HasValue ? TimeSpan.FromTicks(v.Value) : (TimeSpan?)null)
                    .HasColumnType("bigint");

                entity.HasMany(e => e.Comments)
                    .WithOne(e => e.ProjectTask)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Assignments)
                    .WithOne(a => a.ProjectTask)
                    .HasForeignKey(a => a.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasMany(e => e.Tasks)
                    .WithOne(e => e.Project)
                    .HasForeignKey(e => e.ProjectId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.VariationOrders)
                    .WithOne(e => e.Project)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SnagJob>(entity =>
            {
                entity.HasOne(s => s.Project)
                    .WithMany()
                    .HasForeignKey(s => s.ProjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.SubContractor)
                    .WithMany()
                    .HasForeignKey(s => s.SubContractorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.OriginalTask)
                    .WithMany()
                    .HasForeignKey(s => s.OriginalTaskId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.Severity).HasConversion<string>();
            });

            modelBuilder.Entity<Order>()
                .HasOne<Project>()
                .WithMany()
                .HasForeignKey(o => o.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TimeRecord>()
                .HasOne<Project>()
                .WithMany()
                .HasForeignKey(tr => tr.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TimeRecord>()
                .HasOne<ProjectTask>()
                .WithMany()
                .HasForeignKey(tr => tr.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubContractor>(entity =>
            {
                entity.Property(e => e.Rating).HasPrecision(18, 2);
                entity.Property(e => e.OnTimeRate).HasPrecision(18, 2);
            });

            // Chat Configuration
            modelBuilder.Entity<ChatSessionUser>()
                .HasOne(csu => csu.ChatSession)
                .WithMany(cs => cs.SessionUsers)
                .HasForeignKey(csu => csu.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatSessionUser>()
                .HasOne(csu => csu.User)
                .WithMany()
                .HasForeignKey(csu => csu.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.ChatSession)
                .WithMany(cs => cs.Messages)
                .HasForeignKey(cm => cm.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany()
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessageAttachment>()
                .HasOne(cma => cma.Message)
                .WithMany(cm => cm.Attachments)
                .HasForeignKey(cma => cma.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessageReadReceipt>()
                .HasOne(cmrr => cmrr.Message)
                .WithMany(cm => cm.ReadReceipts)
                .HasForeignKey(cmrr => cmrr.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessageReadReceipt>()
                .HasOne(cmrr => cmrr.User)
                .WithMany()
                .HasForeignKey(cmrr => cmrr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Apply Global Query Filter and Concurrency Config for BaseEntity types
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    // Global Query Filter (Soft Delete)
                    var filterMethod = typeof(AppDbContext).GetMethod(nameof(SetGlobalQueryFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        ?.MakeGenericMethod(entityType.ClrType);
                    filterMethod?.Invoke(null, new object[] { modelBuilder });

                    // Global Concurrency Token configuration
                    modelBuilder.Entity(entityType.ClrType)
                        .Property("RowVersion")
                        .IsRowVersion()
                        .IsRequired(false);
                }
            }
        }

        private static void SetGlobalQueryFilter<T>(ModelBuilder modelBuilder) where T : BaseEntity
        {
            modelBuilder.Entity<T>().HasQueryFilter(e => e.IsActive);
        }
    }


    public class AuditEntry
    {
        public AuditEntry(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            Entry = entry;
        }

        public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry { get; }
        public string UserId { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, object?> KeyValues { get; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> OldValues { get; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> NewValues { get; } = new Dictionary<string, object?>();
        public List<Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry> TemporaryProperties { get; } = new List<Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry>();
        public List<string> ChangedColumns { get; } = new List<string>();
        public bool HasTemporaryProperties => TemporaryProperties.Any();
        public string AuditType { get; set; } = string.Empty;

        public AuditLog ToAuditLog()
        {
            var audit = new AuditLog();
            audit.UserId = UserId;
            audit.Action = AuditType; // Create, Update, Delete
            audit.TableName = TableName;
            audit.Timestamp = DateTime.UtcNow;
            audit.RecordId = System.Text.Json.JsonSerializer.Serialize(KeyValues);
            audit.OldValues = OldValues.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(OldValues);
            audit.NewValues = NewValues.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(NewValues);
            return audit;
        }
    }
}
