using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
        /// Opens Outlook/Default Email client with populated To, Subject, Body, and PDF attachment directly attached and brought to front.
        /// </summary>
        public static bool OpenEmailWithAttachment(string recipient, string subject, string body, string attachmentPath)
        {
            // 1. Try Direct COM Interop via CLSID / ProgID with Inspector Activation
            try
            {
                Type? outlookType = Type.GetTypeFromCLSID(new Guid("0006F03A-0000-0000-C000-000000004664"))
                                 ?? Type.GetTypeFromProgID("Outlook.Application");

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

                        mailItem.Display(false); // Display composer

                        // Force window to foreground
                        try
                        {
                            dynamic inspector = mailItem.GetInspector;
                            if (inspector != null)
                            {
                                inspector.Activate();
                            }
                        }
                        catch { }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Direct Outlook COM failed: {ex.Message}");
            }

            // 2. Try Simple MAPI (Win32 MAPI32.dll - Windows Native Mail Client Launcher with Attachment)
            try
            {
                if (SimpleMapi.SendMail(recipient, subject, body, attachmentPath))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SimpleMapi failed: {ex.Message}");
            }

            // 3. Try PowerShell COM Script with Inspector Activation
            if (TrySendViaPowerShellOutlook(recipient, subject, body, attachmentPath))
            {
                return true;
            }

            // 4. Fallback: mailto URI scheme
            try
            {
                var escapedRecipient = Uri.EscapeDataString(recipient ?? string.Empty);
                var escapedSubject = Uri.EscapeDataString(subject ?? string.Empty);
                var escapedBody = Uri.EscapeDataString(body ?? string.Empty);

                var mailtoUri = $"mailto:{escapedRecipient}?subject={escapedSubject}&body={escapedBody}";
                Process.Start(new ProcessStartInfo(mailtoUri) { UseShellExecute = true });

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

        private static bool TrySendViaPowerShellOutlook(string recipient, string subject, string body, string attachmentPath)
        {
            try
            {
                var scriptPath = Path.Combine(Path.GetTempPath(), $"send_email_{Guid.NewGuid():N}.ps1");
                
                var scriptContent = $@"
$outlook = New-Object -ComObject Outlook.Application
$mail = $outlook.CreateItem(0)
$mail.To = '{recipient.Replace("'", "''")}'
$mail.Subject = '{subject.Replace("'", "''")}'
$mail.Body = @'
{body}
'@
if (Test-Path '{attachmentPath.Replace("'", "''")}') {{
    $mail.Attachments.Add('{attachmentPath.Replace("'", "''")}')
}}
$mail.Display()
$inspector = $mail.GetInspector
$inspector.Activate()
";
                File.WriteAllText(scriptPath, scriptContent);

                var psi = new ProcessStartInfo("powershell.exe")
                {
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                process?.WaitForExit(5000);

                Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
                });

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PowerShell Outlook launch failed: {ex.Message}");
                return false;
            }
        }
    }

    internal static class SimpleMapi
    {
        [DllImport("mapi32.dll", CharSet = CharSet.Ansi)]
        private static extern int MAPISendMail(IntPtr session, IntPtr hwnd, MapiMessage message, int flags, int reserved);

        private const int MAPI_LOGON_UI = 0x00000001;
        private const int MAPI_DIALOG = 0x00000008;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class MapiMessage
        {
            public int reserved;
            public string? subject;
            public string? noteText;
            public string? messageType;
            public string? dateReceived;
            public string? conversationID;
            public int flags;
            public IntPtr originator;
            public int recipCount;
            public IntPtr recips;
            public int fileCount;
            public IntPtr files;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class MapiFileDesc
        {
            public int reserved;
            public int flags;
            public int position;
            public string path = string.Empty;
            public string fileName = string.Empty;
            public IntPtr type;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class MapiRecipDesc
        {
            public int reserved;
            public int recipClass;
            public string name = string.Empty;
            public string address = string.Empty;
            public int optionFlags;
            public IntPtr entryID;
        }

        public static bool SendMail(string recipient, string subject, string body, string attachmentPath)
        {
            try
            {
                var msg = new MapiMessage
                {
                    subject = subject,
                    noteText = body
                };

                if (!string.IsNullOrEmpty(recipient))
                {
                    var recip = new MapiRecipDesc
                    {
                        recipClass = 1, // MAPI_TO
                        name = recipient,
                        address = "SMTP:" + recipient
                    };
                    msg.recipCount = 1;
                    msg.recips = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MapiRecipDesc)));
                    Marshal.StructureToPtr(recip, msg.recips, false);
                }

                if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                {
                    var fileDesc = new MapiFileDesc
                    {
                        position = -1,
                        path = attachmentPath,
                        fileName = Path.GetFileName(attachmentPath)
                    };
                    msg.fileCount = 1;
                    msg.files = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MapiFileDesc)));
                    Marshal.StructureToPtr(fileDesc, msg.files, false);
                }

                int result = MAPISendMail(IntPtr.Zero, IntPtr.Zero, msg, MAPI_LOGON_UI | MAPI_DIALOG, 0);

                if (msg.recips != IntPtr.Zero) Marshal.FreeHGlobal(msg.recips);
                if (msg.files != IntPtr.Zero) Marshal.FreeHGlobal(msg.files);

                return result == 0 || result == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MAPISendMail failed: {ex.Message}");
                return false;
            }
        }
    }
}
