using System;

namespace OCC.Shared.DTOs
{
    public class PersonalTodoDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsComplete { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string? OutlookEventId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class CreatePersonalTodoDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime? DueDate { get; set; }
        public string? OutlookEventId { get; set; }
    }

    public class UpdatePersonalTodoDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsComplete { get; set; }
        public string? OutlookEventId { get; set; }
    }
}
