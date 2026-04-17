using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
// using System.Reflection; // Reflection for generic CSV?
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace OCC.Client.Services
{
    public class ExportService : IExportService
    {
        public async Task ExportToCsvAsync<T>(IEnumerable<T> data, string filePath)
        {
            // Simple generic CSV export using properties
            var props = typeof(T).GetProperties();
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",", props.Select(p => p.Name)));

            // Rows
            foreach (var item in data)
            {
                var values = props.Select(p =>
                {
                    var val = p.GetValue(item)?.ToString() ?? "";
                    // Escape commas
                    if (val.Contains(",")) val = $"\"{val}\"";
                    return val;
                });
                sb.AppendLine(string.Join(",", values));
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());
        }

        public async Task<string> GenerateHtmlReportAsync<T>(IEnumerable<T> data, string title, Dictionary<string, string> columns)
        {
            // columns: Key = PropertyName, Value = Header Title
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 40px; color: #333; }");
            sb.AppendLine(".header { display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #F97637; padding-bottom: 10px; margin-bottom: 20px; }");
            sb.AppendLine(".title { font-size: 24px; font-weight: bold; color: #F97637; }");
            sb.AppendLine(".meta { font-size: 14px; color: #666; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
            sb.AppendLine("th, td { border: 1px solid #eee; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #f8f9fa; font-weight: 600; color: #444; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #fafafa; }");
            sb.AppendLine("</style></head><body>");
            
            sb.AppendLine("<div class='header'>");
            sb.AppendLine($"<div class='title'>{title}</div>");
            sb.AppendLine($"<div class='meta'>Generated: {DateTime.Now:dd MMM yyyy HH:mm}</div>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            foreach (var header in columns.Values)
            {
                sb.AppendLine($"<th>{header}</th>");
            }
            sb.AppendLine("</tr></thead><tbody>");

            var type = typeof(T);
            foreach (var item in data)
            {
                sb.AppendLine("<tr>");
                foreach (var propName in columns.Keys)
                {
                    var prop = type.GetProperty(propName);
                    var val = prop?.GetValue(item)?.ToString() ?? "";
                    sb.AppendLine($"<td>{val}</td>");
                }
                sb.AppendLine("</tr>");
            }
            
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</body></html>");

            var tempFile = Path.Combine(Path.GetTempPath(), $"Report_{Guid.NewGuid()}.html");
            await File.WriteAllTextAsync(tempFile, sb.ToString());
            return tempFile;
        }

        public async Task<string> GenerateIndividualStaffReportAsync<T>(OCC.Shared.Models.Employee employee, DateTime start, DateTime end, IEnumerable<T> data, Dictionary<string, string> summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Staff Performance Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;800&display=swap');");
            sb.AppendLine("body { font-family: 'Inter', system-ui, -apple-system, sans-serif; padding: 40px; background: #f8fafc; color: #334155; margin: 0; }");
            sb.AppendLine(".container { max-width: 1000px; margin: 0 auto; background: white; padding: 40px; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); }");
            
            // Header
            sb.AppendLine(".header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 40px; border-bottom: 2px solid #e2e8f0; padding-bottom: 20px; }");
            sb.AppendLine(".brand h1 { margin: 0; font-size: 24px; color: #F97637; font-weight: 800; letter-spacing: -0.5px; }"); // OCC Orange
            sb.AppendLine(".brand p { margin: 5px 0 0; color: #64748b; font-size: 14px; }");
            sb.AppendLine(".meta { text-align: right; font-size: 13px; color: #94a3b8; }");

            // Employee Info
            sb.AppendLine(".emp-section { background: #f1f5f9; padding: 20px; border-radius: 8px; margin-bottom: 30px; display: flex; justify-content: space-between; align-items: center; }");
            sb.AppendLine(".emp-name { font-size: 20px; font-weight: 700; color: #0f172a; margin: 0; }");
            sb.AppendLine(".emp-id { font-size: 14px; color: #64748b; font-weight: 400; margin-left: 10px; }");
            sb.AppendLine(".period-badge { background: white; padding: 6px 16px; border-radius: 100px; font-size: 13px; font-weight: 600; color: #475569; border: 1px solid #e2e8f0; }");

            // Summary Grid
            sb.AppendLine(".summary-grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 15px; margin-bottom: 40px; }");
            sb.AppendLine(".summary-card { background: white; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px; text-align: center; }");
            sb.AppendLine(".summary-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; color: #64748b; margin-bottom: 8px; font-weight: 600; }");
            sb.AppendLine(".summary-value { font-size: 24px; font-weight: 700; color: #0f172a; }");
            sb.AppendLine(".summary-card.highlight { background: #fff7ed; border-color: #ffedd5; }"); // Orange tint
            sb.AppendLine(".summary-card.highlight .summary-value { color: #c2410c; }");

            // Table
            sb.AppendLine("table { width: 100%; border-collapse: separate; border-spacing: 0; margin-bottom: 30px; font-size: 14px; }");
            sb.AppendLine("th { background: #f8fafc; padding: 12px 16px; text-align: left; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; border-top: 1px solid #e2e8f0; }");
            sb.AppendLine("th:first-child { border-top-left-radius: 6px; border-left: 1px solid #e2e8f0; }");
            sb.AppendLine("th:last-child { border-top-right-radius: 6px; border-right: 1px solid #e2e8f0; }");
            sb.AppendLine("td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; color: #334155; }");
            sb.AppendLine("tr:last-child td { border-bottom: none; }");
            sb.AppendLine("tr:hover td { background: #f8fafc; }");
            sb.AppendLine("td:first-child { border-left: 1px solid #e2e8f0; }");
            sb.AppendLine("td:last-child { border-right: 1px solid #e2e8f0; }");

            // Footer
            sb.AppendLine(".footer { text-align: center; margin-top: 60px; padding-top: 20px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8; }");
            
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<div class='container'>");
            
            // Header
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<div class='brand'><h1>Orange Circle Construction</h1><p>Staff Performance Report</p></div>");
            sb.AppendLine($"<div class='meta'>Generated<br><b>{DateTime.Now:dd MMM yyyy}</b><br>{DateTime.Now:HH:mm}</div>");
            sb.AppendLine("</div>");

            // Employee Info
            sb.AppendLine("<div class='emp-section'>");
            sb.AppendLine($"<div><h2 class='emp-name'>{employee.DisplayName}<span class='emp-id'>#{employee.EmployeeNumber}</span></h2></div>");
            sb.AppendLine($"<div class='period-badge'>{start:dd MMM yyyy} - {end:dd MMM yyyy}</div>");
            sb.AppendLine("</div>");

            // Summary Grid
            sb.AppendLine("<div class='summary-grid'>");
            foreach (var kvp in summary)
            {
                var isPay = kvp.Key.Contains("Pay") || kvp.Key.Contains("Wage");
                var isAbsence = kvp.Key.Contains("Absence");
                var highlightClass = isPay ? "highlight" : "";
                
                // If absence > 0 red logic? We can rely on css classes if needed but for now simple
                
                sb.AppendLine($"<div class='summary-card {highlightClass}'>");
                sb.AppendLine($"<div class='summary-label'>{kvp.Key}</div>");
                sb.AppendLine($"<div class='summary-value'>{kvp.Value}</div>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");

            // Table
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Date</th><th>Clock In</th><th>Clock Out</th><th>Status</th><th>Hours</th><th>Wage</th></tr></thead>");
            sb.AppendLine("<tbody>");
            
            var type = typeof(T);
            var props = type.GetProperties();
            
            // Expected property order from ViewModel anonymous object: Date, In, Out, Status, Hours, Wage
            // We can iterate dynamically or hardcode if we know the shape. 
            // The previous implementation utilized dynamic reflection so we stick to it but ensure styling.
            
            foreach (var item in data)
            {
                sb.AppendLine("<tr>");
                foreach(var p in props)
                {
                    sb.AppendLine($"<td>{p.GetValue(item)}</td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");

            sb.AppendLine("<div class='footer'>Confidential Internal Report &bull; Orange Circle Construction</div>");
            
            sb.AppendLine("</div>"); // End Container
            sb.AppendLine("</body></html>");

            var tempFile = Path.Combine(Path.GetTempPath(), $"StaffReport_{employee.LastName}_{Guid.NewGuid()}.html");
            await File.WriteAllTextAsync(tempFile, sb.ToString());
            return tempFile;
        }

        public async Task<string> GenerateEmployeeProfileHtmlAsync(OCC.Shared.Models.Employee employee)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Employee Profile</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;800&display=swap');");
            sb.AppendLine("body { font-family: 'Inter', system-ui, -apple-system, sans-serif; padding: 40px; background: #f8fafc; color: #334155; margin: 0; }");
            sb.AppendLine(".container { max-width: 900px; margin: 0 auto; background: white; padding: 50px; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); }");
            
            // Header
            sb.AppendLine(".header { display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #e2e8f0; padding-bottom: 20px; margin-bottom: 30px; }");
            sb.AppendLine(".brand h1 { margin: 0; font-size: 24px; color: #F97637; font-weight: 800; }");
            sb.AppendLine(".brand p { margin: 5px 0 0; color: #64748b; font-size: 14px; }");
            sb.AppendLine(".emp-title { text-align: right; }");
            sb.AppendLine(".emp-name { margin: 0; font-size: 22px; font-weight: 700; color: #0f172a; }");
            sb.AppendLine(".emp-number { color: #94a3b8; font-size: 14px; }");
            
            // Sections
            sb.AppendLine(".section { margin-bottom: 40px; }");
            sb.AppendLine(".section-title { font-size: 16px; font-weight: 700; color: #334155; border-bottom: 1px solid #e2e8f0; padding-bottom: 8px; margin-bottom: 15px; text-transform: uppercase; letter-spacing: 0.5px; }");
            
            // Grid
            sb.AppendLine(".grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }");
            sb.AppendLine(".grid-3 { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 20px; }");
            
            // Field
            sb.AppendLine(".field { margin-bottom: 15px; }");
            sb.AppendLine(".label { display: block; font-size: 11px; text-transform: uppercase; color: #64748b; font-weight: 600; margin-bottom: 4px; }");
            sb.AppendLine(".value { font-size: 15px; color: #0f172a; font-weight: 500; }");
            
            // Footer
            sb.AppendLine(".footer { margin-top: 50px; pt: 20px; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #94a3b8; }");
            
            sb.AppendLine("</style></head><body>");
            
            sb.AppendLine("<div class='container'>");
            
            // Header
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<div class='brand'><h1>Orange Circle Construction</h1><p>Employee Profile</p></div>");
            sb.AppendLine($"<div class='emp-title'><h2 class='emp-name'>{employee.DisplayName}</h2><span class='emp-number'>{employee.EmployeeNumber}</span></div>");
            sb.AppendLine("</div>");

            // Personal Info
            sb.AppendLine("<div class='section'><div class='section-title'>Personal Information</div>");
            sb.AppendLine("<div class='grid-2'>");
            sb.AppendLine($"<div class='field'><span class='label'>First Name</span><div class='value'>{employee.FirstName}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Last Name</span><div class='value'>{employee.LastName}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>ID / Passport</span><div class='value'>{employee.IdNumber} ({employee.IdType})</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Date of Birth</span><div class='value'>{employee.DoB:dd MMM yyyy}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Email</span><div class='value'>{employee.Email}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Phone</span><div class='value'>{employee.Phone}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Physical Address</span><div class='value'>{employee.PhysicalAddress}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Tax Number</span><div class='value'>{employee.TaxNumber}</div></div>");
            sb.AppendLine("</div></div>");
            
            // Employment Info
            sb.AppendLine("<div class='section'><div class='section-title'>Employment Details</div>");
            sb.AppendLine("<div class='grid-2'>");
            sb.AppendLine($"<div class='field'><span class='label'>Role</span><div class='value'>{employee.Role}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Employment Type</span><div class='value'>{employee.EmploymentType}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Start Date</span><div class='value'>{employee.EmploymentDate:dd MMM yyyy}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Branch</span><div class='value'>{employee.Branch}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Remuneration</span><div class='value'>{employee.RateType} @ R{employee.HourlyRate:N2}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Shift Hours</span><div class='value'>{employee.ShiftStartTime:hh\\:mm} - {employee.ShiftEndTime:hh\\:mm}</div></div>");
            sb.AppendLine("</div></div>");

            // Emergency / Next of Kin
            sb.AppendLine("<div class='section'><div class='section-title'>Next of Kin & Emergency Contacts</div>");
            sb.AppendLine("<div class='grid-2'>");
            sb.AppendLine($"<div class='field'><span class='label'>Next of Kin</span><div class='value'>{employee.NextOfKinName}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Relationship</span><div class='value'>{employee.NextOfKinRelation}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Phone</span><div class='value'>{employee.NextOfKinPhone}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Emergency Contact (Alt)</span><div class='value'>{employee.EmergencyContactName}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Emergency Phone</span><div class='value'>{employee.EmergencyContactPhone}</div></div>");
            sb.AppendLine("</div></div>");
            
            // Banking
            sb.AppendLine("<div class='section'><div class='section-title'>Banking Details</div>");
            sb.AppendLine("<div class='grid-2'>");
            sb.AppendLine($"<div class='field'><span class='label'>Bank</span><div class='value'>{employee.BankName}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Account Number</span><div class='value'>{employee.AccountNumber}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Branch Code</span><div class='value'>{employee.BranchCode}</div></div>");
            sb.AppendLine($"<div class='field'><span class='label'>Account Type</span><div class='value'>{employee.AccountType}</div></div>");
            sb.AppendLine("</div></div>");

            sb.AppendLine($"<div class='footer'>Generated on {DateTime.Now:dd MMM yyyy} &bull; Orange Circle Construction</div>");
            sb.AppendLine("</div>"); // container
            sb.AppendLine("</body></html>");

            var tempFile = Path.Combine(Path.GetTempPath(), $"Profile_{employee.FirstName}_{employee.LastName}_{Guid.NewGuid()}.html");
            await File.WriteAllTextAsync(tempFile, sb.ToString());
            return tempFile;
        }

        public async Task<string> GenerateAuditDeviationReportAsync(OCC.Shared.Models.HseqAudit audit, IEnumerable<OCC.Shared.Models.HseqAuditNonComplianceItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Audit Deviation Action Sheet</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700;800&display=swap');");
            sb.AppendLine("body { font-family: 'Inter', system-ui, -apple-system, sans-serif; padding: 40px; background: #f1f5f9; color: #1e293b; margin: 0; }");
            sb.AppendLine(".container { max-width: 1100px; margin: 0 auto; background: white; padding: 50px; border-radius: 16px; box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1); border: 1px solid #e2e8f0; }");
            
            // Header
            sb.AppendLine(".header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 40px; border-bottom: 3px solid #F97637; padding-bottom: 25px; }");
            sb.AppendLine(".brand h1 { margin: 0; font-size: 28px; color: #F97637; font-weight: 800; letter-spacing: -1px; text-transform: uppercase; }");
            sb.AppendLine(".brand p { margin: 5px 0 0; color: #64748b; font-size: 16px; font-weight: 600; }");
            sb.AppendLine(".meta { text-align: right; }");
            sb.AppendLine(".meta-item { font-size: 13px; color: #64748b; margin-bottom: 4px; }");
            sb.AppendLine(".meta-value { font-weight: 700; color: #0f172a; font-size: 15px; }");

            // Audit Info Grid
            sb.AppendLine(".info-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 25px; margin-bottom: 40px; background: #f8fafc; padding: 25px; border-radius: 12px; border: 1px solid #e2e8f0; }");
            sb.AppendLine(".info-item { display: flex; flex-direction: column; }");
            sb.AppendLine(".info-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.05em; color: #64748b; font-weight: 700; margin-bottom: 6px; }");
            sb.AppendLine(".info-value { font-size: 15px; color: #0f172a; font-weight: 600; }");

            // Table
            sb.AppendLine("table { width: 100%; border-collapse: separate; border-spacing: 0; margin-bottom: 30px; border-radius: 12px; overflow: hidden; border: 1px solid #e2e8f0; }");
            sb.AppendLine("th { background: #f8fafc; padding: 18px 20px; text-align: left; font-weight: 700; color: #475569; font-size: 12px; text-transform: uppercase; letter-spacing: 0.05em; border-bottom: 2px solid #e2e8f0; }");
            sb.AppendLine("td { padding: 20px; border-bottom: 1px solid #e2e8f0; color: #334155; vertical-align: top; font-size: 14px; line-height: 1.6; }");
            sb.AppendLine("tr:last-child td { border-bottom: none; }");
            sb.AppendLine("tr:hover td { background: #fcfcfc; }");

            // Status Badges
            sb.AppendLine(".status { display: inline-flex; align-items: center; padding: 6px 12px; border-radius: 100px; font-size: 11px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.05em; color: white; }");
            sb.AppendLine(".status-green { background: #22C55E; }");
            sb.AppendLine(".status-yellow { background: #EAB308; color: #854d0e; }");
            sb.AppendLine(".status-orange { background: #F97316; }");
            sb.AppendLine(".status-red { background: #EF4444; }");

            // Footer
            sb.AppendLine(".footer { text-align: center; margin-top: 60px; padding-top: 40px; border-top: 2px solid #e2e8f0; font-size: 13px; color: #94a3b8; font-weight: 500; }");
            
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<div class='container'>");
            
            // Header
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<div class='brand'><h1>Orange Circle Construction</h1><p>AUDIT DEVIATION ACTION SHEET / CLOSE OUT REPORT</p></div>");
            sb.AppendLine("<div class='meta'>");
            sb.AppendLine($"<div class='meta-item'>Audit Ref: <span class='meta-value'>{audit.AuditNumber}</span></div>");
            sb.AppendLine($"<div class='meta-item'>Date: <span class='meta-value'>{audit.Date:dd MMM yyyy}</span></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            // Info Grid
            sb.AppendLine("<div class='info-grid'>");
            sb.AppendLine($"<div class='info-item'><span class='info-label'>Site Name</span><span class='info-value'>{audit.SiteName}</span></div>");
            sb.AppendLine($"<div class='info-item'><span class='info-label'>Site Manager</span><span class='info-value'>{audit.SiteManager}</span></div>");
            sb.AppendLine($"<div class='info-item'><span class='info-label'>HSEQ Consultant</span><span class='info-value'>{audit.HseqConsultant}</span></div>");
            sb.AppendLine($"<div class='info-item'><span class='info-label'>Status</span><span class='info-value'>{audit.Status}</span></div>");
            sb.AppendLine($"<div class='info-item'><span class='info-label'>Efficiency Rating</span><span class='info-value'>{audit.ActualScore:N2}%</span></div>");
            sb.AppendLine($"<div class='info-item'><span class='info-label'>Close Out Date</span><span class='info-value'>{(audit.CloseOutDate.HasValue ? audit.CloseOutDate.Value.ToString("dd MMM yyyy") : "Pending")}</span></div>");
            sb.AppendLine("</div>");

            // Table
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th width='15%'>Item on Audit</th><th width='35%'>Finding</th><th width='15%'>Responsible</th><th width='12%'>Completion Date</th><th width='13%'>Status</th></tr></thead>");
            sb.AppendLine("<tbody>");
            
            foreach (var item in items)
            {
                var colorClass = "status-red";
                if (item.Status == OCC.Shared.Enums.AuditItemStatus.Closed)
                {
                    colorClass = "status-green";
                }
                else if (item.TargetDate.HasValue)
                {
                    var daysUntil = (item.TargetDate.Value.Date - DateTime.Today).TotalDays;
                    if (daysUntil <= 7) colorClass = "status-yellow";
                    else if (daysUntil <= 30) colorClass = "status-orange";
                    else colorClass = "status-orange";
                }

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td><strong>{item.RegulationReference}</strong></td>");
                sb.AppendLine($"<td>{item.Description}<br/><br/><div style='font-size: 12px; color: #64748b;'><strong>Corrective Action:</strong><br/>{item.CorrectiveAction}</div></td>");
                sb.AppendLine($"<td>{item.ResponsiblePerson}</td>");
                sb.AppendLine($"<td>{(item.TargetDate.HasValue ? item.TargetDate.Value.ToString("dd MMM yyyy") : "TBD")}</td>");
                sb.AppendLine($"<td><span class='status {colorClass}'>{item.Status}</span></td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");

            sb.AppendLine("<div class='footer'>Confidential Internal Report &bull; Orange Circle Construction &bull; " + DateTime.Now.ToString("yyyy") + "</div>");
            
            sb.AppendLine("</div>"); // container
            sb.AppendLine("</body></html>");

            var tempFile = Path.Combine(Path.GetTempPath(), $"AuditCloseOut_{audit.AuditNumber}_{Guid.NewGuid()}.html");
            await File.WriteAllTextAsync(tempFile, sb.ToString());
            return tempFile;
        }

        public Task OpenFileAsync(string filePath)
        {
            try
            {
                var p = new ProcessStartInfo(filePath) { UseShellExecute = true };
                Process.Start(p);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening file: {ex.Message}");
            }
            return Task.CompletedTask;
        }
    }
}
