using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;

using OCC.Shared.Enums;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a specific unit of work or activity within a <see cref="Project"/>.
    /// Supports hierarchical structures (Gantt chart style), dependencies, and resource assignment.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>ProjectTasks</c> table.
    /// <b>How:</b> Tasks belong to a <see cref="Project"/> and can be grouped via <see cref="ParentId"/> / <see cref="Children"/>.
    /// Time tracked against tasks feeds into project costing.
    /// </remarks>
    public class ProjectTask : BaseEntity, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }



        private Guid? _ownerId;
        public Guid? OwnerId 
        { 
            get => _ownerId; 
            set { if (_ownerId != value) { _ownerId = value; OnPropertyChanged(); } } 
        }

        private string? _legacyId;
        public string? LegacyId 
        { 
            get => _legacyId; 
            set { if (_legacyId != value) { _legacyId = value; OnPropertyChanged(); } } 
        }

        private string _name = string.Empty;
        [Required]
        public string Name 
        { 
            get => _name; 
            set { if (_name != value) { _name = value; OnPropertyChanged(); } } 
        }

        private DateTime _startDate;
        public DateTime StartDate 
        { 
            get => _startDate; 
            set { if (_startDate != value) { _startDate = value; OnPropertyChanged(); } } 
        }

        private DateTime _finishDate;
        public DateTime FinishDate 
        { 
            get => _finishDate; 
            set { if (_finishDate != value) { _finishDate = value; OnPropertyChanged(); } } 
        }
        
        private string _duration = string.Empty;
        public string Duration 
        { 
            get => _duration; 
            set { if (_duration != value) { _duration = value; OnPropertyChanged(); } } 
        }

        private int _percentComplete;
        public int PercentComplete 
        { 
            get => _percentComplete; 
            set 
            { 
                if (_percentComplete != value) 
                { 
                    _percentComplete = value; 
                    UpdateStatusFromPercent();
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(IsComplete)); 
                } 
            } 
        }

        private string _priority = "Medium";
        public string Priority 
        { 
            get => _priority; 
            set { if (_priority != value) { _priority = value; OnPropertyChanged(); } } 
        }

        #region Reminders
        /// <summary> Whether a reminder is active for this task. </summary>
        public bool IsReminderSet { get; set; }

        /// <summary> How often the reminder should repeat. </summary>
        public ReminderFrequency Frequency { get; set; } = ReminderFrequency.Once;

        /// <summary> The date/time of the next reminder. </summary>
        public DateTime? NextReminderDate { get; set; }

        /// <summary> Tracked for real-time synchronization order. </summary>
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
        #endregion

        private string _status = "Not Started";
        public string Status 
        { 
            get => _status; 
            set 
            { 
                if (_status != value) 
                { 
                    _status = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(IsComplete)); 
                } 
            } 
        }

        [NotMapped]
        public bool IsComplete 
        {
            get => Status == "Completed" || Status == "Done" || PercentComplete == 100;
            set
            {
                if (value)
                {
                    Status = "Completed";
                    PercentComplete = 100;
                }
                else
                {
                    if (PercentComplete == 100) PercentComplete = 50; 
                    UpdateStatusFromPercent();
                    ActualCompleteDate = null;
                }
                OnPropertyChanged();
            }
        }

        private void UpdateStatusFromPercent()
        {
            if (IsOnHold) return;
            if (PercentComplete >= 100) Status = "Completed";
            else if (PercentComplete >= 75) Status = "Almost Done";
            else if (PercentComplete >= 50) Status = "Halfway";
            else if (PercentComplete > 0) Status = "Started";
            else Status = "Not Started";
        }

        [NotMapped]
        public bool IsOverdue => !IsComplete && FinishDate < DateTime.Now && Status != "Cancelled";

        [NotMapped]
        public string StatusColor 
        {
            get
            {
                if (IsOnHold) return "#FFC107"; // Yellow (SecondaryGold)
                if (IsOverdue) return "#FF4B4B"; // Red (ErrorRed)
                
                switch (Status)
                {
                    case "Not Started": 
                    case "To Do": return "#A0FFFFFF"; // Grey (TextSub)
                    case "Started": 
                    case "In Progress": return "#2E9DFF"; // Blue (AccentBlue)
                    case "Halfway": return "#8B5CF6"; // Violet
                    case "Almost Done": return "#EC4899"; // Pink
                    case "Done": 
                    case "Completed": return "#00C853"; // Green (SuccessGreen)
                    case "On Hold": return "#FFC107"; // Yellow (SecondaryGold)
                    default: 
                        if (PercentComplete >= 100) return "#00C853";
                        if (PercentComplete >= 75) return "#EC4899";
                        if (PercentComplete >= 50) return "#8B5CF6";
                        if (PercentComplete > 0) return "#2E9DFF";
                        return "#A0FFFFFF";
                }
            }
        }

        /// <summary> Detailed description or instructions for the task. </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary> If work is temporarily suspended, why is it on hold? </summary>
        public string HoldReason { get; set; } = string.Empty;

        /// <summary> The category of the activity (Task vs Meeting). </summary>
        public TaskType Type { get; set; } = TaskType.Task;

        /// <summary> Gets a comma-separated string of initials for all assigned staff. </summary>
        [NotMapped]
        public string AssigneeInitials => Assignments == null || !Assignments.Any() 
            ? "--" 
            : string.Join(", ", Assignments.Select(a => string.Concat(a.AssigneeName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0]))).Select(s => s.ToUpper()));

        /// <summary> User comments and discussion thread attached to this task. </summary>
        public List<TaskComment> Comments { get; set; } = new();

        /// <summary> Resources (employees/teams) assigned to this task. </summary>
        public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
        
        private bool _isOnHold;
        /// <summary> If true, work is temporarily suspended. </summary>
        public bool IsOnHold 
        { 
            get => _isOnHold; 
            set 
            { 
                if (_isOnHold != value) 
                { 
                    _isOnHold = value; 
                    if (_isOnHold)
                    {
                        Status = "On Hold";
                    }
                    else if (Status == "On Hold")
                    {
                        UpdateStatusFromPercent();
                    }
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(StatusColor)); 
                } 
            } 
        }

        /// <summary> Foreign Key to the parent <see cref="Models.Project"/>. Nullable for Personal Tasks. </summary>
        public Guid? ProjectId { get; set; }
        
        /// <summary> Navigation property to the parent Project. </summary>
        public virtual Project? Project { get; set; }
        
        /// <summary> Navigation property to the parent task (if any). </summary>
        public virtual ProjectTask? ParentTask { get; set; }

        /// <summary> Optional Foreign Key to a parent task (for nested sub-tasks). </summary>
        public Guid? ParentId { get; set; }

        /// <summary> Collection of sub-tasks. </summary>
        public List<ProjectTask> Children { get; set; } = new();

        /// <summary> List of dependency task IDs (Predecessors). </summary>
        public List<string> Predecessors { get; set; } = new();

        /// <summary> The visual sort order index for display. </summary>
        public int OrderIndex { get; set; }

        /// <summary> visual indentation level for hierarchical display (0 is root). </summary>
        public int IndentLevel { get; set; }

        /// <summary> If true, this is a summary/container task with children. </summary>
        public bool IsGroup { get; set; }
        
        // UI Helpers
        private bool _isExpanded = true;
        
        /// <summary> UI State: whether the sub-tasks are visible. </summary>
        public bool IsExpanded 
        { 
            get => _isExpanded; 
            set 
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary> Helper to check if the task is a parent node. </summary>
        public bool HasChildren => Children != null && Children.Any();

        #region Actuals
        /// <summary> The actual date work commenced. </summary>
        public DateTime? ActualStartDate { get; set; }
        /// <summary> The actual date work was completed. </summary>
        public DateTime? ActualCompleteDate { get; set; }
        /// <summary> The actual time taken to complete the task. </summary>
        public TimeSpan? ActualDuration { get; set; }
        /// <summary> Planned duration in hours (stored as nullable TimeSpan). </summary>
        public TimeSpan? PlannedDurationHours { get; set; }

        /// <summary> Legacy property to support old database schema. </summary>
        public string AssignedTo { get; set; } = string.Empty;

        #endregion

        #region Geofencing
        /// <summary> GPS Latitude for verifying task location (if applicable). </summary>
        public double? Latitude { get; set; }
        /// <summary> GPS Longitude for verifying task location (if applicable). </summary>
        public double? Longitude { get; set; }
        #endregion
    }

    /// <summary>
    /// Differentiates between standard work tasks and scheduled meetings.
    /// </summary>
    public enum TaskType
    {
        /// <summary> A standard work activity. </summary>
        Task,
        /// <summary> A scheduled meeting or consultation. </summary>
        Meeting,
        /// <summary> A quick personal to-do item. </summary>
        PersonalToDo
    }
}
