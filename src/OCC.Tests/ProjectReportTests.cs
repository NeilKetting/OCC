using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Features.ProjectHub.ViewModels;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using Xunit;

namespace OCC.Tests
{
    public class ProjectReportTests
    {
        private readonly Mock<IProjectService> _mockProjectService;
        private readonly Mock<IHealthSafetyService> _mockHealthSafetyService;
        private readonly Mock<ISubContractorService> _mockSubContractorService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly Mock<IDialogService> _mockDialogService;
        private readonly Mock<IPdfService> _mockPdfService;
        private readonly Mock<IProjectReportService> _mockProjectReportService;
        private readonly ConnectionSettings _connectionSettings;

        public ProjectReportTests()
        {
            _mockProjectService = new Mock<IProjectService>();
            _mockHealthSafetyService = new Mock<IHealthSafetyService>();
            _mockSubContractorService = new Mock<ISubContractorService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockAuthService = new Mock<IAuthService>();
            _mockDialogService = new Mock<IDialogService>();
            _mockPdfService = new Mock<IPdfService>();
            _mockProjectReportService = new Mock<IProjectReportService>();
            _connectionSettings = new ConnectionSettings { ApiBaseUrl = "http://localhost:5237/" };

            // Setup default behaviors to avoid null refs
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
            _mockHealthSafetyService.Setup(s => s.GetAuditsAsync()).ReturnsAsync(new List<AuditSummaryDto>());
            _mockHealthSafetyService.Setup(s => s.GetIncidentsAsync()).ReturnsAsync(new List<IncidentSummaryDto>());
            _mockSubContractorService.Setup(s => s.GetSubContractorsAsync()).ReturnsAsync(new List<SubContractor>());
            _mockProjectReportService.Setup(s => s.GetDraftAsync(It.IsAny<Guid>())).ReturnsAsync((ProjectReportDraft?)null);
            _mockProjectReportService.Setup(s => s.GetHistoryAsync(It.IsAny<Guid>())).ReturnsAsync(new List<ProjectReportHistory>());
        }

        [Fact]
        public async Task LoadReportDataAsync_CalculatesCorrectWeekNumber_ForThursdayCycle()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            // Test 1: Future start date should yield week 1
            var futureProject = new Project
            {
                Id = projectId,
                Name = "Future Project",
                StartDate = DateTime.Today.AddDays(10),
                EndDate = DateTime.Today.AddDays(20)
            };

            _mockProjectService.Setup(s => s.GetProjectAsync(projectId)).ReturnsAsync(futureProject);
            _mockProjectService.Setup(s => s.GetProjectTasksAsync(projectId)).ReturnsAsync(new List<ProjectTask>());

            var viewModel = new ProjectReportViewModel(
                _mockProjectService.Object,
                _mockHealthSafetyService.Object,
                _mockSubContractorService.Object,
                _mockHttpClientFactory.Object,
                _mockAuthService.Object,
                _connectionSettings,
                _mockDialogService.Object,
                NullLogger<ProjectReportViewModel>.Instance,
                _mockPdfService.Object,
                _mockProjectReportService.Object);

            // Act
            await viewModel.LoadReportDataAsync(projectId);

            // Assert
            Assert.Equal(1, viewModel.WeekNumber);

            // Test 2: Past start date. We find a Thursday in the past dynamically.
            var startThursday = DateTime.Today.AddDays(-14);
            while (startThursday.DayOfWeek != DayOfWeek.Thursday)
            {
                startThursday = startThursday.AddDays(1);
            }

            var pastProject = new Project
            {
                Id = projectId,
                Name = "Past Project",
                StartDate = startThursday,
                EndDate = DateTime.Today
            };

            _mockProjectService.Setup(s => s.GetProjectAsync(projectId)).ReturnsAsync(pastProject);
            
            // Act
            await viewModel.LoadReportDataAsync(projectId);

            // Assert
            var daysSinceFirstThursday = (DateTime.Today - startThursday).Days;
            var expectedWeek = DateTime.Today <= startThursday ? 1 : ((daysSinceFirstThursday - 1) / 7) + 2;
            Assert.Equal(expectedWeek, viewModel.WeekNumber);
        }

        [Fact]
        public async Task LoadReportDataAsync_CalculatesPOWRequiredProgress_BasedOnLeafTasksPlannedProgress()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                Name = "POW Test Project",
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(10)
            };

            var tasks = new List<ProjectTask>
            {
                // Task 1: Complete (FinishDate in past) - planned progress 100%
                new ProjectTask
                {
                    Name = "Task 1",
                    IsGroup = false,
                    StartDate = DateTime.Today.AddDays(-5),
                    FinishDate = DateTime.Today.AddDays(-1),
                    PercentComplete = 100
                },
                // Task 2: Active (Middle) - planned progress 50%
                new ProjectTask
                {
                    Name = "Task 2",
                    IsGroup = false,
                    StartDate = DateTime.Today.AddDays(-5),
                    FinishDate = DateTime.Today.AddDays(5),
                    PercentComplete = 40
                },
                // Task 3: Future - planned progress 0%
                new ProjectTask
                {
                    Name = "Task 3",
                    IsGroup = false,
                    StartDate = DateTime.Today.AddDays(1),
                    FinishDate = DateTime.Today.AddDays(5),
                    PercentComplete = 0
                },
                // Task 4: Parent Group task - should be ignored in leaf-task POW calculation
                new ProjectTask
                {
                    Name = "Parent Task",
                    IsGroup = true,
                    StartDate = DateTime.Today.AddDays(-5),
                    FinishDate = DateTime.Today.AddDays(5),
                    PercentComplete = 50
                }
            };

            _mockProjectService.Setup(s => s.GetProjectAsync(projectId)).ReturnsAsync(project);
            _mockProjectService.Setup(s => s.GetProjectTasksAsync(projectId)).ReturnsAsync(tasks);

            var viewModel = new ProjectReportViewModel(
                _mockProjectService.Object,
                _mockHealthSafetyService.Object,
                _mockSubContractorService.Object,
                _mockHttpClientFactory.Object,
                _mockAuthService.Object,
                _connectionSettings,
                _mockDialogService.Object,
                NullLogger<ProjectReportViewModel>.Instance,
                _mockPdfService.Object,
                _mockProjectReportService.Object);

            // Act
            await viewModel.LoadReportDataAsync(projectId);

            // Assert
            // Expected planned progress: (100 + 50 + 0) / 3 = 50%
            Assert.Equal(50.0, viewModel.PowPercentRequired, 2);
        }

        [Fact]
        public async Task LoadReportDataAsync_CategorizesMilestonesCorrectly()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                Name = "Milestone Category Project",
                StartDate = DateTime.Today.AddDays(-20),
                EndDate = DateTime.Today.AddDays(20)
            };

            var today = DateTime.Today;
            var workingDays = new List<DateTime>();
            var temp = today;
            while (workingDays.Count < 5)
            {
                if (temp.DayOfWeek != DayOfWeek.Saturday && temp.DayOfWeek != DayOfWeek.Sunday)
                {
                    workingDays.Add(temp);
                }
                temp = temp.AddDays(1);
            }
            var minWorkingDay = workingDays[0];
            var maxWorkingDay = workingDays[4];

            var tasks = new List<ProjectTask>
            {
                // 1. This week milestone (IsGroup = true, FinishDate within min/max working days)
                new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    Name = "This Week Milestone",
                    IsGroup = true,
                    StartDate = minWorkingDay,
                    FinishDate = minWorkingDay.AddDays(1),
                    PercentComplete = 40,
                    IsComplete = false
                },
                // 2. Overdue milestone (IsGroup = true, FinishDate < today, IsComplete = false)
                new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    Name = "Overdue Milestone",
                    IsGroup = true,
                    StartDate = today.AddDays(-10),
                    FinishDate = today.AddDays(-2),
                    PercentComplete = 20,
                    IsComplete = false
                },
                // 3. Completed past milestone (IsGroup = true, FinishDate < today, IsComplete = true) - should NOT show up anywhere
                new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    Name = "Completed Past Milestone",
                    IsGroup = true,
                    StartDate = today.AddDays(-10),
                    FinishDate = today.AddDays(-2),
                    PercentComplete = 100,
                    IsComplete = true
                },
                // 4. Non-group task in reporting week - should NOT show up as a milestone
                new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    Name = "Leaf Task This Week",
                    IsGroup = false,
                    StartDate = minWorkingDay,
                    FinishDate = minWorkingDay.AddDays(1),
                    PercentComplete = 0,
                    IsComplete = false
                },
                // 5. Active this week, but finishes next week - should show up in this week's milestones
                new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    Name = "Active Finishes Next Week Milestone",
                    IsGroup = true,
                    StartDate = minWorkingDay.AddDays(1),
                    FinishDate = maxWorkingDay.AddDays(2),
                    PercentComplete = 10,
                    IsComplete = false
                },
                // 6. Active this week, ends on Friday/max working day with time component - should show up in this week's milestones
                new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    Name = "Thursday Time Component Milestone",
                    IsGroup = true,
                    StartDate = minWorkingDay,
                    FinishDate = maxWorkingDay.AddHours(12),
                    PercentComplete = 30,
                    IsComplete = false
                }
            };

            _mockProjectService.Setup(s => s.GetProjectAsync(projectId)).ReturnsAsync(project);
            _mockProjectService.Setup(s => s.GetProjectTasksAsync(projectId)).ReturnsAsync(tasks);

            var viewModel = new ProjectReportViewModel(
                _mockProjectService.Object,
                _mockHealthSafetyService.Object,
                _mockSubContractorService.Object,
                _mockHttpClientFactory.Object,
                _mockAuthService.Object,
                _connectionSettings,
                _mockDialogService.Object,
                NullLogger<ProjectReportViewModel>.Instance,
                _mockPdfService.Object,
                _mockProjectReportService.Object);

            // Act
            await viewModel.LoadReportDataAsync(projectId);

            // Assert
            Assert.Equal(3, viewModel.ThisWeekMilestones.Count);
            var thisWeekNames = viewModel.ThisWeekMilestones.Select(m => m.Name).ToList();
            Assert.Contains("This Week Milestone", thisWeekNames);
            Assert.Contains("Thursday Time Component Milestone", thisWeekNames);
            Assert.Contains("Active Finishes Next Week Milestone", thisWeekNames);

            Assert.Single(viewModel.OverdueMilestones);
            Assert.Equal("Overdue Milestone", viewModel.OverdueMilestones.First().Name);
        }

        [Fact]
        public async Task LoadReportDataAsync_LoadsAndSavesOverdueMilestoneReasons()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                Name = "Reasons Project",
                StartDate = DateTime.Today.AddDays(-20),
                EndDate = DateTime.Today.AddDays(20)
            };

            var today = DateTime.Today;
            var workingDays = new List<DateTime>();
            var temp = today;
            while (workingDays.Count < 5)
            {
                if (temp.DayOfWeek != DayOfWeek.Saturday && temp.DayOfWeek != DayOfWeek.Sunday)
                {
                    workingDays.Add(temp);
                }
                temp = temp.AddDays(1);
            }
            var minWorkingDay = workingDays[0];
            var maxWorkingDay = workingDays[4];

            var milestoneId1 = Guid.NewGuid();
            var milestoneId2 = Guid.NewGuid();

            var tasks = new List<ProjectTask>
            {
                new ProjectTask
                {
                    Id = milestoneId1,
                    Name = "Milestone 1",
                    IsGroup = true,
                    StartDate = minWorkingDay,
                    FinishDate = minWorkingDay.AddDays(2),
                    PercentComplete = 30,
                    IsComplete = false
                },
                new ProjectTask
                {
                    Id = milestoneId2,
                    Name = "Milestone 2",
                    IsGroup = true,
                    StartDate = today.AddDays(-10),
                    FinishDate = today.AddDays(-2),
                    PercentComplete = 10,
                    IsComplete = false
                }
            };

            var reasonsMap = new Dictionary<Guid, string>
            {
                { milestoneId1, "Rain delay" },
                { milestoneId2, "Waiting for materials" }
            };
            var jsonReasons = System.Text.Json.JsonSerializer.Serialize(reasonsMap);

            var draft = new ProjectReportDraft
            {
                ProjectId = projectId,
                OverdueMilestoneReasons = jsonReasons,
                StatusSummary = "Summary info"
            };

            _mockProjectService.Setup(s => s.GetProjectAsync(projectId)).ReturnsAsync(project);
            _mockProjectService.Setup(s => s.GetProjectTasksAsync(projectId)).ReturnsAsync(tasks);
            _mockProjectReportService.Setup(s => s.GetDraftAsync(projectId)).ReturnsAsync(draft);

            var viewModel = new ProjectReportViewModel(
                _mockProjectService.Object,
                _mockHealthSafetyService.Object,
                _mockSubContractorService.Object,
                _mockHttpClientFactory.Object,
                _mockAuthService.Object,
                _connectionSettings,
                _mockDialogService.Object,
                NullLogger<ProjectReportViewModel>.Instance,
                _mockPdfService.Object,
                _mockProjectReportService.Object);

            // Act: Load
            await viewModel.LoadReportDataAsync(projectId);

            // Assert load
            var thisWeekMilestoneItem = viewModel.ThisWeekMilestones.FirstOrDefault(m => m.TaskId == milestoneId1);
            var overdueMilestoneItem = viewModel.OverdueMilestones.FirstOrDefault(m => m.TaskId == milestoneId2);
            
            Assert.NotNull(thisWeekMilestoneItem);
            Assert.Equal("Rain delay", thisWeekMilestoneItem.Reason);
            
            Assert.NotNull(overdueMilestoneItem);
            Assert.Equal("Waiting for materials", overdueMilestoneItem.Reason);

            // Modify a reason and execute save
            thisWeekMilestoneItem.Reason = "Updated rain delay";
            
            ProjectReportDraft? savedDraft = null;
            _mockProjectReportService
                .Setup(s => s.SaveDraftAsync(projectId, It.IsAny<ProjectReportDraft>()))
                .Callback<Guid, ProjectReportDraft>((id, d) => savedDraft = d)
                .ReturnsAsync(true);

            // Act: Save
            viewModel.SaveCommand.Execute(null);

            // Assert save
            Assert.NotNull(savedDraft);
            Assert.Equal("Summary info", savedDraft.StatusSummary);
            
            var savedReasons = System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, string>>(savedDraft.OverdueMilestoneReasons);
            Assert.NotNull(savedReasons);
            Assert.Equal("Updated rain delay", savedReasons[milestoneId1]);
            Assert.Equal("Waiting for materials", savedReasons[milestoneId2]);
        }

        [Fact]
        public async Task LoadReportDataAsync_OnlyIncludesDirectChildrenOfProjectTaskAsMilestones()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                Name = "Engen Kroonvaal North",
                StartDate = DateTime.Today.AddDays(-20),
                EndDate = DateTime.Today.AddDays(20)
            };

            var today = DateTime.Today;
            var workingDays = new List<DateTime>();
            var temp = today;
            while (workingDays.Count < 5)
            {
                if (temp.DayOfWeek != DayOfWeek.Saturday && temp.DayOfWeek != DayOfWeek.Sunday)
                {
                    workingDays.Add(temp);
                }
                temp = temp.AddDays(1);
            }
            var minWorkingDay = workingDays[0];
            var maxWorkingDay = workingDays[4];

            var rootTaskId = Guid.NewGuid();
            var projectTaskId = Guid.NewGuid();
            var milestone1Id = Guid.NewGuid();
            var subTask1Id = Guid.NewGuid();
            var milestone2Id = Guid.NewGuid();

            var tasks = new List<ProjectTask>
            {
                // Level 0: Root program version task (IsGroup = true, ParentId = null)
                new ProjectTask
                {
                    Id = rootTaskId,
                    Name = "Kroonvaal North POW 1.1",
                    IsGroup = true,
                    ParentId = null,
                    StartDate = minWorkingDay,
                    FinishDate = maxWorkingDay,
                    PercentComplete = 10,
                    IsComplete = false
                },
                // Level 1: Project task matching project name (IsGroup = true, ParentId = rootTaskId)
                new ProjectTask
                {
                    Id = projectTaskId,
                    Name = "Engen Kroonvaal North",
                    IsGroup = true,
                    ParentId = rootTaskId,
                    StartDate = minWorkingDay,
                    FinishDate = maxWorkingDay,
                    PercentComplete = 10,
                    IsComplete = false
                },
                // Level 2: Direct child milestone (IsGroup = true, ParentId = projectTaskId)
                new ProjectTask
                {
                    Id = milestone1Id,
                    Name = "Site Establishment",
                    IsGroup = true,
                    ParentId = projectTaskId,
                    StartDate = minWorkingDay,
                    FinishDate = minWorkingDay.AddDays(2),
                    PercentComplete = 100,
                    IsComplete = true
                },
                // Level 2: Direct child milestone (IsGroup = true, ParentId = projectTaskId) - due this week
                new ProjectTask
                {
                    Id = milestone2Id,
                    Name = "Demolition",
                    IsGroup = true,
                    ParentId = projectTaskId,
                    StartDate = minWorkingDay,
                    FinishDate = minWorkingDay.AddDays(4),
                    PercentComplete = 20,
                    IsComplete = false
                },
                // Level 3: Nested sub-task of Demolition (IsGroup = true, ParentId = milestone2Id)
                new ProjectTask
                {
                    Id = subTask1Id,
                    Name = "Quickshop",
                    IsGroup = true,
                    ParentId = milestone2Id,
                    StartDate = minWorkingDay,
                    FinishDate = minWorkingDay.AddDays(2),
                    PercentComplete = 20,
                    IsComplete = false
                }
            };

            _mockProjectService.Setup(s => s.GetProjectAsync(projectId)).ReturnsAsync(project);
            _mockProjectService.Setup(s => s.GetProjectTasksAsync(projectId)).ReturnsAsync(tasks);

            var viewModel = new ProjectReportViewModel(
                _mockProjectService.Object,
                _mockHealthSafetyService.Object,
                _mockSubContractorService.Object,
                _mockHttpClientFactory.Object,
                _mockAuthService.Object,
                _connectionSettings,
                _mockDialogService.Object,
                NullLogger<ProjectReportViewModel>.Instance,
                _mockPdfService.Object,
                _mockProjectReportService.Object);

            // Act
            await viewModel.LoadReportDataAsync(projectId);

            // Assert
            Assert.Equal(2, viewModel.ThisWeekMilestones.Count);
            var thisWeekNames = viewModel.ThisWeekMilestones.Select(m => m.Name).ToList();
            Assert.Contains("Site Establishment", thisWeekNames);
            Assert.Contains("Demolition", thisWeekNames);
            Assert.DoesNotContain("Kroonvaal North POW 1.1", thisWeekNames);
            Assert.DoesNotContain("Engen Kroonvaal North", thisWeekNames);
            Assert.DoesNotContain("Quickshop", thisWeekNames);
        }
    }
}
