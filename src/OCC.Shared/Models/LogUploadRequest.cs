using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    public class LogUploadRequest
    {
        [Key]
        public Guid Id { get; set; }

        // User Info
        public Guid? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;

        // Machine Info
        public string MachineName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty; // e.g. "Microsoft Windows 10.0.19045"
        public string DotNetVersion { get; set; } = string.Empty; // e.g. "8.0.1"
        public string ProcessorCount { get; set; } = string.Empty; // e.g. "12"
        public string SystemMemory { get; set; } = string.Empty; // Approximate available memory

        // App Info
        public string AppVersion { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty; // e.g. "Production" or "Staging"
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public string FilePath { get; set; } = string.Empty; // Server-side path storage
        
        // Culture Info (To diagnose date format issues)
        public string CultureName { get; set; } = string.Empty; // e.g. "en-ZA"
        public string DatePattern { get; set; } = string.Empty; // e.g. "dd/MM/yyyy"
    }
}
