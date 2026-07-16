using System;

namespace OCC.Shared.Models
{
    public enum Branch
    {
        JHB,
        CPT
    }

    public static class BranchExtensions
    {
        public static Branch? ToBranchEnum(this string? branchStr)
        {
            if (string.IsNullOrWhiteSpace(branchStr)) return null;

            var trimmed = branchStr.Trim();
            if (trimmed.Contains("Cape", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(trimmed, "CPT", StringComparison.OrdinalIgnoreCase))
            {
                return Branch.CPT;
            }

            if (trimmed.Contains("Johannesburg", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(trimmed, "JHB", StringComparison.OrdinalIgnoreCase))
            {
                return Branch.JHB;
            }

            return null;
        }
    }
}

