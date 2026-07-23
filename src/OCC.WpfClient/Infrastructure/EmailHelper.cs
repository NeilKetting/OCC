using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OCC.WpfClient.Infrastructure
{
    public static class EmailHelper
    {
        /// <summary>
        /// Parses raw email string containing one or multiple email addresses delimited by ;, ,, |, or spaces.
        /// </summary>
        public static List<string> ParseEmailAddresses(string? rawEmails)
        {
            if (string.IsNullOrWhiteSpace(rawEmails)) return new List<string>();

            var delimiters = new[] { ';', ',', '|', '/', '\n', '\r', ' ' };
            var parts = rawEmails.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            var validEmails = new List<string>();
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed) && trimmed.Contains('@') && trimmed.Contains('.'))
                {
                    if (!validEmails.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                    {
                        validEmails.Add(trimmed);
                    }
                }
            }
            return validEmails;
        }

        /// <summary>
        /// Opens Outlook (via COM Interop) or default system mail client with populated To, Subject, Body, and PDF attachment.
        /// </summary>
        public static bool OpenEmailWithAttachment(string recipient, string subject, string body, string attachmentPath)
        {
            // 1. Try Outlook COM Interop (standard for Windows Desktop with Office/Outlook installed)
            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType != null)
                {
                    dynamic outlookApp = Activator.CreateInstance(outlookType)!;
                    if (outlookApp != null)
                    {
                        dynamic mailItem = outlookApp.CreateItem(0); // 0 = olMailItem
                        mailItem.To = recipient ?? string.Empty;
                        mailItem.Subject = subject ?? string.Empty;
                        mailItem.Body = body ?? string.Empty;

                        if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                        {
                            mailItem.Attachments.Add(attachmentPath);
                        }

                        mailItem.Display(false); // Display Outlook email composer window
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Outlook COM Interop unavailable: {ex.Message}");
            }

            // 2. Fallback: mailto URI scheme
            try
            {
                var escapedRecipient = Uri.EscapeDataString(recipient ?? string.Empty);
                var escapedSubject = Uri.EscapeDataString(subject ?? string.Empty);
                var escapedBody = Uri.EscapeDataString(body ?? string.Empty);

                var mailtoUri = $"mailto:{escapedRecipient}?subject={escapedSubject}&body={escapedBody}";
                Process.Start(new ProcessStartInfo(mailtoUri) { UseShellExecute = true });

                // Open file explorer with the PDF file pre-selected for manual drag & drop if needed
                if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{attachmentPath}\"");
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"mailto URI launch failed: {ex.Message}");
                return false;
            }
        }
    }
}
