using System;
using System.Collections.Generic;
using OCC.Shared.Models;

namespace OCC.Shared.DTOs
{
    public class AuditLogsResponseDto
    {
        public List<AuditLog> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int CreateCount { get; set; }
        public int UpdateCount { get; set; }
        public int DeleteCount { get; set; }
    }
}
