using System.Collections.Generic;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Static-like class containing hardcoded company information for Orange Circle Construction.
    /// Used for populating invoices, reports, and UI footers.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Not persisted in a standard database table (usually just one shared instance or hardcoded config).
    /// <b>How:</b> Provides properties for Company Name, Reg No, VAT No, and details per <see cref="Branch"/>.
    /// Backward compatibility properties (like <see cref="Email"/>, <see cref="Phone"/>) default to the JHB branch.
    /// </remarks>
    public class CompanyDetails
    {
        public string CompanyName { get; set; } = "Orange Circle Construction (PTY) Ltd";
        public string RegistrationNumber { get; set; } = "2007/004038/23";
        public string VatNumber { get; set; } = "4510254610";
        public string Website { get; set; } = string.Empty;

        /// <summary> Global default interest rate for employee loans (percentage, e.g. 5.0 for 5%). </summary>
        public decimal GlobalLoanInterestRate { get; set; } = 0;

        /// <summary> Global toggle for automatic daily clock-in system. </summary>
        public bool AutoClockInEnabled { get; set; } = false;

        /// <summary> Which days of the week auto clock-in should run. </summary>
        public List<System.DayOfWeek> AutoClockInDays { get; set; } = new();

        // --- Backwards Compatibility Wrappers (Default to JHB) ---

        /// <summary> Default department emails (defaults to JHB). </summary>
        public List<DepartmentEmail> DepartmentEmails 
        { 
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].DepartmentEmails : new List<DepartmentEmail>(); 
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].DepartmentEmails = value; } 
        }

        /// <summary> Default company email address (defaults to JHB). </summary>
        public string Email
        {
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].Email : string.Empty;
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].Email = value; }
        }

        /// <summary> Default company phone number (defaults to JHB). </summary>
        public string Phone
        {
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].Phone : string.Empty;
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].Phone = value; }
        }

        /// <summary> Default company fax number (defaults to JHB). </summary>
        public string Fax
        {
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].Fax : string.Empty;
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].Fax = value; }
        }

        /// <summary> Single line address string (defaults to JHB). </summary>
        public string Address
        {
            get => Branches.ContainsKey(Branch.JHB) ? $"{Branches[Branch.JHB].AddressLine1}, {Branches[Branch.JHB].City}, {Branches[Branch.JHB].PostalCode}" : string.Empty;
        }

        /// <summary> Alias for Address. </summary>
        public string FullAddress => Address;

        // --- Banking Details ---
        public string BankName { get; set; } = "FNB";
        public string AccountName { get; set; } = "Orange Circle Construction";
        public string AccountNumber { get; set; } = "62123456789";
        public string BranchCode { get; set; } = "250655";
        public string AccountType { get; set; } = "Business Account";

        // --- Address Component Wrappers (Default to JHB) ---
        public string AddressLine1
        {
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].AddressLine1 : string.Empty;
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].AddressLine1 = value; }
        }

        public string AddressLine2
        {
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].AddressLine2 : string.Empty;
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].AddressLine2 = value; }
        }

        public string City
        {
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].City : string.Empty;
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].City = value; }
        }

        public string PostalCode
        {
            get => Branches.ContainsKey(Branch.JHB) ? Branches[Branch.JHB].PostalCode : string.Empty;
            set { if (Branches.ContainsKey(Branch.JHB)) Branches[Branch.JHB].PostalCode = value; }
        }

        /// <summary>
        /// Dictionary storing details for each supported branch (JHB, CPT, etc.).
        /// </summary>
        public Dictionary<Branch, BranchDetails> Branches { get; set; } = new()
        {
            { 
                Branch.JHB, new BranchDetails 
                { 
                    Phone = "+27 11 391-1340",
                    Fax = "(011) 972-6958",
                    Email = "info@orange-circle.co.za",
                    AddressLine1 = "No. 58, Rd5, Wydan Business Park",
                    AddressLine2 = "Brentwood Park",
                    City = "Benoni",
                    PostalCode = "1510",
                    DepartmentEmails = new()
                    {
                        new DepartmentEmail { Department = "Buying", EmailAddress = "bernard@orange-circle.co.za" },
                        new DepartmentEmail { Department = "Accounts", EmailAddress = "anthia@orange-circle.co.za" }
                    }
                } 
            },
            { 
                Branch.CPT, new BranchDetails 
                { 
                    Phone = "TBD",
                    Fax = "TBD",
                    Email = "TBD",
                    AddressLine1 = "TBD",
                    AddressLine2 = "TBD",
                    City = "Cape Town",
                    PostalCode = "TBD",
                    ShiftStartTime = new TimeSpan(7,0,0),
                    ShiftEndTime = new TimeSpan(16,45,0),
                    DepartmentEmails = new()
                    {
                        new DepartmentEmail { Department = "Buying", EmailAddress = "TBD" },
                        new DepartmentEmail { Department = "Accounts", EmailAddress = "TBD" }
                    }
                } 
            }
        };
    }

    /// <summary>
    /// Details specific to a physical branch office.
    /// </summary>
    public class BranchDetails
    {
        public string Phone { get; set; } = string.Empty;
        public string Fax { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "South Africa";

        public TimeSpan ShiftStartTime { get; set; } = new TimeSpan(7, 0, 0); // 07:00
        public TimeSpan ShiftEndTime { get; set; } = new TimeSpan(16, 45, 0); // 16:45

        public List<DepartmentEmail> DepartmentEmails { get; set; } = new();

        public string FullAddress => $"{AddressLine1}, {City}, {PostalCode}";
    }

    /// <summary>
    /// Maps a department name to a specific email address.
    /// </summary>
    public class DepartmentEmail
    {
        public string Department { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
    }
}
