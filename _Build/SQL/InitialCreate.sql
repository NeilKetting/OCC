IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AppSettings] (
    [Id] uniqueidentifier NOT NULL,
    [Key] nvarchar(max) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Id])
);

CREATE TABLE [AttendanceRecords] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [EmployeeId] uniqueidentifier NULL,
    [Date] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [CheckInTime] datetime2 NULL,
    [CheckOutTime] datetime2 NULL,
    [HoursWorked] float NOT NULL,
    [Notes] nvarchar(max) NULL,
    [LeaveReason] nvarchar(max) NULL,
    [DoctorsNoteImagePath] nvarchar(max) NULL,
    [Branch] nvarchar(max) NOT NULL,
    [ClockInTime] time NULL,
    [CachedHourlyRate] decimal(18,2) NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([Id])
);

CREATE TABLE [AuditLogs] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(max) NOT NULL,
    [Action] nvarchar(max) NOT NULL,
    [TableName] nvarchar(max) NOT NULL,
    [RecordId] nvarchar(max) NOT NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [BugReports] (
    [Id] uniqueidentifier NOT NULL,
    [ReporterId] uniqueidentifier NULL,
    [ReporterName] nvarchar(max) NOT NULL,
    [ReportedDate] datetime2 NOT NULL,
    [ViewName] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [AdminComments] nvarchar(max) NULL,
    [ScreenshotBase64] nvarchar(max) NULL,
    CONSTRAINT [PK_BugReports] PRIMARY KEY ([Id])
);

CREATE TABLE [Customers] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Header] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
);

CREATE TABLE [Employees] (
    [Id] uniqueidentifier NOT NULL,
    [LinkedUserId] uniqueidentifier NULL,
    [RateType] int NOT NULL,
    [HourlyRate] float NOT NULL,
    [TaxNumber] nvarchar(max) NOT NULL,
    [AnnualLeaveBalance] float NOT NULL,
    [SickLeaveBalance] float NOT NULL,
    [LeaveCycleStartDate] datetime2 NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [IdType] int NOT NULL,
    [IdNumber] nvarchar(max) NOT NULL,
    [PermitNumber] nvarchar(max) NULL,
    [Email] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [DoB] datetime2 NOT NULL,
    [Role] int NOT NULL,
    [EmployeeNumber] nvarchar(max) NOT NULL,
    [EmploymentType] int NOT NULL,
    [ContractDuration] nvarchar(max) NULL,
    [EmploymentDate] datetime2 NOT NULL,
    [Branch] nvarchar(max) NOT NULL,
    [ShiftStartTime] time NULL,
    [ShiftEndTime] time NULL,
    [BankName] nvarchar(max) NULL,
    [AccountNumber] nvarchar(max) NULL,
    [BranchCode] nvarchar(max) NULL,
    [AccountType] nvarchar(max) NULL,
    [LeaveBalance] float NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
);

CREATE TABLE [HseqAudits] (
    [Id] uniqueidentifier NOT NULL,
    [Date] datetime2 NOT NULL,
    [SiteName] nvarchar(max) NOT NULL,
    [ScopeOfWorks] nvarchar(max) NOT NULL,
    [SiteManager] nvarchar(max) NOT NULL,
    [SiteSupervisor] nvarchar(max) NOT NULL,
    [HseqConsultant] nvarchar(max) NOT NULL,
    [AuditNumber] nvarchar(max) NOT NULL,
    [TargetScore] decimal(18,2) NOT NULL,
    [ActualScore] decimal(18,2) NOT NULL,
    [Findings] nvarchar(max) NOT NULL,
    [NonConformance] nvarchar(max) NOT NULL,
    [ImmediateAction] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [CloseOutDate] datetime2 NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_HseqAudits] PRIMARY KEY ([Id])
);

CREATE TABLE [HseqDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Category] int NOT NULL,
    [FilePath] nvarchar(max) NOT NULL,
    [UploadedBy] nvarchar(max) NOT NULL,
    [UploadDate] datetime2 NOT NULL,
    [Version] nvarchar(max) NOT NULL,
    [FileSize] nvarchar(max) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_HseqDocuments] PRIMARY KEY ([Id])
);

CREATE TABLE [HseqSafeHourRecords] (
    [Id] uniqueidentifier NOT NULL,
    [Month] datetime2 NOT NULL,
    [SafeWorkHours] float NOT NULL,
    [IncidentReported] nvarchar(max) NOT NULL,
    [NearMisses] int NOT NULL,
    [RootCause] nvarchar(max) NOT NULL,
    [CorrectiveActions] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ReportedBy] nvarchar(max) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_HseqSafeHourRecords] PRIMARY KEY ([Id])
);

CREATE TABLE [HseqTrainingRecords] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeName] nvarchar(max) NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [Role] nvarchar(max) NOT NULL,
    [TrainingTopic] nvarchar(max) NOT NULL,
    [DateCompleted] datetime2 NOT NULL,
    [ValidUntil] datetime2 NULL,
    [Trainer] nvarchar(max) NOT NULL,
    [CertificateUrl] nvarchar(max) NOT NULL,
    [CertificateType] nvarchar(max) NOT NULL,
    [ExpiryWarningDays] int NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_HseqTrainingRecords] PRIMARY KEY ([Id])
);

CREATE TABLE [Incidents] (
    [Id] uniqueidentifier NOT NULL,
    [Date] datetime2 NOT NULL,
    [Type] int NOT NULL,
    [Severity] int NOT NULL,
    [Location] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [ReportedByUserId] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [InvestigatorId] nvarchar(max) NOT NULL,
    [RootCause] nvarchar(max) NOT NULL,
    [CorrectiveAction] nvarchar(max) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Incidents] PRIMARY KEY ([Id])
);

CREATE TABLE [InventoryItems] (
    [Id] uniqueidentifier NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Supplier] nvarchar(max) NOT NULL,
    [Category] nvarchar(max) NOT NULL,
    [Location] nvarchar(max) NOT NULL,
    [JhbQuantity] float NOT NULL,
    [CptQuantity] float NOT NULL,
    [QuantityOnHand] float NOT NULL,
    [ReorderPoint] float NOT NULL,
    [UnitOfMeasure] nvarchar(max) NOT NULL,
    [Sku] nvarchar(max) NOT NULL,
    [AverageCost] decimal(18,2) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [TrackLowStock] bit NOT NULL,
    [IsStockItem] bit NOT NULL,
    CONSTRAINT [PK_InventoryItems] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationDismissals] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [NotificationType] nvarchar(max) NOT NULL,
    [DismissedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_NotificationDismissals] PRIMARY KEY ([Id])
);

CREATE TABLE [Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [IsRead] bit NOT NULL,
    [Type] int NOT NULL,
    [TargetAction] nvarchar(max) NULL,
    [UserId] uniqueidentifier NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
);

CREATE TABLE [PublicHolidays] (
    [Id] uniqueidentifier NOT NULL,
    [Date] datetime2 NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_PublicHolidays] PRIMARY KEY ([Id])
);

CREATE TABLE [Suppliers] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [PostalCode] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [ContactPerson] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [VatNumber] nvarchar(max) NOT NULL,
    [BankName] nvarchar(max) NOT NULL,
    [BankAccountNumber] nvarchar(max) NOT NULL,
    [BranchCode] nvarchar(max) NOT NULL,
    [SupplierAccountNumber] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
);

CREATE TABLE [Teams] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [LeaderId] uniqueidentifier NULL,
    CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Password] nvarchar(max) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NULL,
    [Location] nvarchar(max) NULL,
    [ProfilePictureBase64] nvarchar(max) NULL,
    [ApproverId] uniqueidentifier NULL,
    [IsApproved] bit NOT NULL,
    [IsEmailVerified] bit NOT NULL,
    [Permissions] nvarchar(max) NULL,
    [Branch] int NULL,
    [UserRole] int NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [WageRuns] (
    [Id] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [RunDate] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_WageRuns] PRIMARY KEY ([Id])
);

CREATE TABLE [BugComments] (
    [Id] uniqueidentifier NOT NULL,
    [BugReportId] uniqueidentifier NOT NULL,
    [AuthorName] nvarchar(max) NOT NULL,
    [AuthorEmail] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsDevComment] bit NOT NULL,
    CONSTRAINT [PK_BugComments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BugComments_BugReports_BugReportId] FOREIGN KEY ([BugReportId]) REFERENCES [BugReports] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [LeaveRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [NumberOfDays] int NOT NULL,
    [LeaveType] int NOT NULL,
    [Status] int NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [AdminComment] nvarchar(max) NULL,
    [ApproverId] uniqueidentifier NULL,
    [ActionedDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL,
    [IsUnpaid] bit NOT NULL,
    CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LeaveRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [OvertimeRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Date] datetime2 NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [RejectionReason] nvarchar(max) NULL,
    [Status] int NOT NULL,
    [ApproverId] uniqueidentifier NULL,
    [ActionedDate] datetime2 NULL,
    [AdminComment] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_OvertimeRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OvertimeRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Projects] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [StreetLine1] nvarchar(max) NOT NULL,
    [StreetLine2] nvarchar(max) NULL,
    [City] nvarchar(max) NOT NULL,
    [StateOrProvince] nvarchar(max) NOT NULL,
    [PostalCode] nvarchar(max) NOT NULL,
    [Country] nvarchar(max) NOT NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [Status] nvarchar(max) NOT NULL,
    [ProjectManager] nvarchar(max) NOT NULL,
    [SiteManagerId] uniqueidentifier NULL,
    [Customer] nvarchar(max) NOT NULL,
    [Priority] nvarchar(max) NOT NULL,
    [ShortName] nvarchar(max) NOT NULL,
    [WorkStartTime] time NOT NULL,
    [WorkEndTime] time NOT NULL,
    [LunchDurationMinutes] int NOT NULL,
    [CustomerId] uniqueidentifier NULL,
    CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Projects_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_Projects_Employees_SiteManagerId] FOREIGN KEY ([SiteManagerId]) REFERENCES [Employees] ([Id])
);

CREATE TABLE [HseqAuditComplianceItems] (
    [Id] uniqueidentifier NOT NULL,
    [AuditId] uniqueidentifier NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [RegulationReference] nvarchar(max) NOT NULL,
    [PhotoBase64] nvarchar(max) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_HseqAuditComplianceItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HseqAuditComplianceItems_HseqAudits_AuditId] FOREIGN KEY ([AuditId]) REFERENCES [HseqAudits] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [HseqAuditNonComplianceItems] (
    [Id] uniqueidentifier NOT NULL,
    [AuditId] uniqueidentifier NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [RegulationReference] nvarchar(max) NOT NULL,
    [PhotoBase64] nvarchar(max) NOT NULL,
    [CorrectiveAction] nvarchar(max) NOT NULL,
    [ResponsiblePerson] nvarchar(max) NOT NULL,
    [TargetDate] datetime2 NULL,
    [ClosedDate] datetime2 NULL,
    [Status] int NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_HseqAuditNonComplianceItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HseqAuditNonComplianceItems_HseqAudits_AuditId] FOREIGN KEY ([AuditId]) REFERENCES [HseqAudits] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [HseqAuditSections] (
    [Id] uniqueidentifier NOT NULL,
    [AuditId] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [PossibleScore] decimal(18,2) NOT NULL,
    [ActualScore] decimal(18,2) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_HseqAuditSections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HseqAuditSections_HseqAudits_AuditId] FOREIGN KEY ([AuditId]) REFERENCES [HseqAudits] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [IncidentPhotos] (
    [Id] uniqueidentifier NOT NULL,
    [IncidentId] uniqueidentifier NOT NULL,
    [Base64Content] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_IncidentPhotos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentPhotos_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TeamMembers] (
    [Id] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DateAdded] datetime2 NOT NULL,
    CONSTRAINT [PK_TeamMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TeamMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [WageRunLines] (
    [Id] uniqueidentifier NOT NULL,
    [WageRunId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeName] nvarchar(max) NOT NULL,
    [Branch] nvarchar(max) NOT NULL,
    [NormalHours] float NOT NULL,
    [OvertimeHours] float NOT NULL,
    [ProjectedHours] float NOT NULL,
    [VarianceHours] float NOT NULL,
    [VarianceNotes] nvarchar(max) NOT NULL,
    [HourlyRate] decimal(18,2) NOT NULL,
    [TotalWage] decimal(18,2) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_WageRunLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WageRunLines_WageRuns_WageRunId] FOREIGN KEY ([WageRunId]) REFERENCES [WageRuns] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Orders] (
    [Id] uniqueidentifier NOT NULL,
    [OrderNumber] nvarchar(max) NOT NULL,
    [OrderDate] datetime2 NOT NULL,
    [ExpectedDeliveryDate] datetime2 NULL,
    [OrderType] int NOT NULL,
    [Branch] int NOT NULL,
    [SupplierId] uniqueidentifier NULL,
    [SupplierName] nvarchar(max) NOT NULL,
    [CustomerId] uniqueidentifier NULL,
    [EntityAddress] nvarchar(max) NOT NULL,
    [EntityTel] nvarchar(max) NOT NULL,
    [EntityVatNo] nvarchar(max) NOT NULL,
    [DestinationType] int NOT NULL,
    [ProjectId] uniqueidentifier NULL,
    [ProjectName] nvarchar(max) NULL,
    [Attention] nvarchar(max) NOT NULL,
    [TaxRate] decimal(18,4) NOT NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    [DeliveryInstructions] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [ProjectTasks] (
    [Id] uniqueidentifier NOT NULL,
    [LegacyId] nvarchar(max) NULL,
    [Name] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [FinishDate] datetime2 NOT NULL,
    [Duration] nvarchar(max) NOT NULL,
    [PercentComplete] int NOT NULL,
    [Priority] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Type] int NOT NULL,
    [IsOnHold] bit NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [Predecessors] nvarchar(max) NOT NULL,
    [OrderIndex] int NOT NULL,
    [IndentLevel] int NOT NULL,
    [IsGroup] bit NOT NULL,
    [IsExpanded] bit NOT NULL,
    [ActualStartDate] datetime2 NULL,
    [ActualCompleteDate] datetime2 NULL,
    [ActualDuration] bigint NULL,
    [PlannedDurationHours] bigint NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    CONSTRAINT [PK_ProjectTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectTasks_ProjectTasks_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectTasks_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TimeRecords] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [EmployeeId] uniqueidentifier NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [TaskId] uniqueidentifier NOT NULL,
    [Date] datetime2 NOT NULL,
    [Hours] float NOT NULL,
    [Comment] nvarchar(max) NULL,
    CONSTRAINT [PK_TimeRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TimeRecords_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [OrderLines] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [InventoryItemId] uniqueidentifier NULL,
    [ItemCode] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Category] nvarchar(max) NOT NULL,
    [QuantityOrdered] float NOT NULL,
    [QuantityReceived] float NOT NULL,
    [UnitOfMeasure] nvarchar(max) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [VatAmount] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderLines_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TaskAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [TaskId] uniqueidentifier NOT NULL,
    [ProjectTaskId] uniqueidentifier NOT NULL,
    [AssigneeId] uniqueidentifier NOT NULL,
    [AssigneeType] int NOT NULL,
    [AssigneeName] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_TaskAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaskAssignments_ProjectTasks_ProjectTaskId] FOREIGN KEY ([ProjectTaskId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TaskAssignments_ProjectTasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TaskComments] (
    [Id] uniqueidentifier NOT NULL,
    [TaskId] uniqueidentifier NOT NULL,
    [ProjectTaskId] uniqueidentifier NULL,
    [AuthorName] nvarchar(max) NOT NULL,
    [AuthorEmail] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaskComments_ProjectTasks_ProjectTaskId] FOREIGN KEY ([ProjectTaskId]) REFERENCES [ProjectTasks] ([Id]),
    CONSTRAINT [FK_TaskComments_ProjectTasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [ProjectTasks] ([Id]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Date', N'Name') AND [object_id] = OBJECT_ID(N'[PublicHolidays]'))
    SET IDENTITY_INSERT [PublicHolidays] ON;
INSERT INTO [PublicHolidays] ([Id], [Date], [Name])
VALUES ('26704267-f3a4-4989-b0a5-37c417b95702', '2026-12-25T00:00:00.0000000', N'Christmas Day'),
('30c918c6-95e5-4421-b312-d2645651bca4', '2026-06-16T00:00:00.0000000', N'Youth Day'),
('38cdd243-4091-4917-b035-b93687168c2f', '2026-04-03T00:00:00.0000000', N'Good Friday'),
('50048563-7e9d-419e-9dff-24f75604f54d', '2026-05-01T00:00:00.0000000', N'Workers'' Day'),
('5938448d-19b8-4a51-ac58-ae0891869e49', '2026-08-09T00:00:00.0000000', N'National Women''s Day'),
('5a57c040-4bff-425e-b530-8b6db459bfc6', '2026-04-27T00:00:00.0000000', N'Freedom Day'),
('678c9669-14ee-4e28-919b-5fe10e57447e', '2026-04-06T00:00:00.0000000', N'Family Day'),
('754204e2-cb75-49a0-8738-132506df58dc', '2026-01-01T00:00:00.0000000', N'New Year''s Day'),
('76223a37-2ba0-42b5-a522-4657343e4617', '2026-08-10T00:00:00.0000000', N'Public Holiday'),
('7f7254c9-6c12-4bc1-af0f-d9ad23c912c7', '2026-09-24T00:00:00.0000000', N'Heritage Day'),
('a689b9e9-4b40-4a38-b0b8-f37f8a2fa620', '2026-12-26T00:00:00.0000000', N'Day of Goodwill'),
('a7830c8a-e891-4bb6-be52-bed140b238ef', '2026-12-16T00:00:00.0000000', N'Day of Reconciliation'),
('b8525598-5c63-41c4-943f-825d90bebb9e', '2026-03-21T00:00:00.0000000', N'Human Rights Day');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Date', N'Name') AND [object_id] = OBJECT_ID(N'[PublicHolidays]'))
    SET IDENTITY_INSERT [PublicHolidays] OFF;

CREATE INDEX [IX_BugComments_BugReportId] ON [BugComments] ([BugReportId]);

CREATE INDEX [IX_HseqAuditComplianceItems_AuditId] ON [HseqAuditComplianceItems] ([AuditId]);

CREATE INDEX [IX_HseqAuditNonComplianceItems_AuditId] ON [HseqAuditNonComplianceItems] ([AuditId]);

CREATE INDEX [IX_HseqAuditSections_AuditId] ON [HseqAuditSections] ([AuditId]);

CREATE INDEX [IX_IncidentPhotos_IncidentId] ON [IncidentPhotos] ([IncidentId]);

CREATE INDEX [IX_LeaveRequests_EmployeeId] ON [LeaveRequests] ([EmployeeId]);

CREATE INDEX [IX_OrderLines_OrderId] ON [OrderLines] ([OrderId]);

CREATE INDEX [IX_Orders_ProjectId] ON [Orders] ([ProjectId]);

CREATE INDEX [IX_OvertimeRequests_EmployeeId] ON [OvertimeRequests] ([EmployeeId]);

CREATE INDEX [IX_Projects_CustomerId] ON [Projects] ([CustomerId]);

CREATE INDEX [IX_Projects_SiteManagerId] ON [Projects] ([SiteManagerId]);

CREATE INDEX [IX_ProjectTasks_ParentId] ON [ProjectTasks] ([ParentId]);

CREATE INDEX [IX_ProjectTasks_ProjectId] ON [ProjectTasks] ([ProjectId]);

CREATE INDEX [IX_TaskAssignments_ProjectTaskId] ON [TaskAssignments] ([ProjectTaskId]);

CREATE INDEX [IX_TaskAssignments_TaskId] ON [TaskAssignments] ([TaskId]);

CREATE INDEX [IX_TaskComments_ProjectTaskId] ON [TaskComments] ([ProjectTaskId]);

CREATE INDEX [IX_TaskComments_TaskId] ON [TaskComments] ([TaskId]);

CREATE INDEX [IX_TeamMembers_TeamId] ON [TeamMembers] ([TeamId]);

CREATE INDEX [IX_TimeRecords_ProjectId] ON [TimeRecords] ([ProjectId]);

CREATE INDEX [IX_WageRunLines_WageRunId] ON [WageRunLines] ([WageRunId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260118053912_InitialCreate', N'9.0.0');

COMMIT;
GO

