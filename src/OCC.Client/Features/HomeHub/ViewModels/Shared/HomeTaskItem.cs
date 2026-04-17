using System;

namespace OCC.Client.Features.HomeHub.ViewModels.Shared
{
    public class HomeTaskItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsCompleted { get; set; }
        public string Title { get; set; } = string.Empty; 
        
        // Aliases for View Compatibility
        public string TaskName => Title;
        public DateTime Due => DueDate;
        public string Project { get; set; } = "Local Project"; // Placeholder
        public string Progress => Status;

        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; } 
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string AssigneeInitials { get; set; } = "??";
        
        public int CommentsCount { get; set; }
        public int AttachmentsCount { get; set; }
    }
}
