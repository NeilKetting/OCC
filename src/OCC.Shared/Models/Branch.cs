using System;

namespace OCC.Shared.Models
{
    public enum Branch
    {
        JHB,
        CPT
    }

    public static class BranchConstants
    {
        public const string All = "All";
        public const string Johannesburg = "Johannesburg";
        public const string CapeTown = "Cape Town";
        public const string JHB = "JHB";
        public const string CPT = "CPT";
    }

    public static class BranchExtensions
    {
        public static Branch? ToBranchEnum(this string? branchStr)
        {
            if (string.IsNullOrWhiteSpace(branchStr)) return null;

            var trimmed = branchStr.Trim();
            if (trimmed.Contains("Cape", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(trimmed, BranchConstants.CPT, StringComparison.OrdinalIgnoreCase))
            {
                return Branch.CPT;
            }

            if (trimmed.Contains("Johannesburg", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(trimmed, BranchConstants.JHB, StringComparison.OrdinalIgnoreCase))
            {
                return Branch.JHB;
            }

            return null;
        }

        public static bool IsCapeTown(this string? branchStr)
            => branchStr.ToBranchEnum() == Branch.CPT;

        public static bool IsJohannesburg(this string? branchStr)
            => branchStr.ToBranchEnum() == Branch.JHB;

        public static bool MatchesBranch(this string? employeeBranch, string? requestedBranch)
        {
            if (string.IsNullOrWhiteSpace(requestedBranch) || requestedBranch.Equals(BranchConstants.All, StringComparison.OrdinalIgnoreCase))
                return true;

            var reqEnum = requestedBranch.ToBranchEnum();
            var empEnum = employeeBranch.ToBranchEnum();

            if (reqEnum.HasValue && empEnum.HasValue)
                return reqEnum.Value == empEnum.Value;

            return string.Equals(employeeBranch?.Trim(), requestedBranch.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

