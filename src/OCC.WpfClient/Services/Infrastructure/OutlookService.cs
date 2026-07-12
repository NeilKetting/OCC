using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OCC.WpfClient.Features.CalendarHub.Models;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace OCC.WpfClient.Services.Infrastructure
{
    public interface IOutlookService
    {
        string? SyncTodoToCalendar(string title, string? notes, DateTime? dueDate, string? existingEventId);
        void DeleteEvent(string? eventId);
        List<CalendarEvent> GetOutlookEvents(DateTime start, DateTime end);
        void CheckUpcomingMeetings(Action<string, DateTime> onMeetingStartingSoon);
    }

    public class OutlookService : IOutlookService
    {
        private readonly LocalSettingsService _localSettings;
        private readonly HashSet<string> _notifiedEventIds = new();

        public OutlookService(LocalSettingsService localSettings)
        {
            _localSettings = localSettings;
        }

        private Outlook.Application? GetOutlookApplication()
        {
            try
            {
                // In COM Interop, calling new Outlook.Application() will bind to the
                // running instance or start a new one automatically.
                return new Outlook.Application();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start/connect to Outlook: {ex.Message}");
                return null;
            }
        }

        public string? SyncTodoToCalendar(string title, string? notes, DateTime? dueDate, string? existingEventId)
        {
            if (_localSettings.Settings.DisableOutlookSync) return existingEventId;
            if (!dueDate.HasValue) return existingEventId;

            Outlook.Application? outlookApp = null;
            Outlook.NameSpace? ns = null;
            Outlook.AppointmentItem? appointment = null;
            try
            {
                outlookApp = GetOutlookApplication();
                if (outlookApp == null) return existingEventId;

                ns = outlookApp.GetNamespace("MAPI");

                if (!string.IsNullOrEmpty(existingEventId))
                {
                    try
                    {
                        appointment = (Outlook.AppointmentItem)ns.GetItemFromID(existingEventId);
                    }
                    catch
                    {
                        // Event might have been deleted in Outlook, leave appointment as null to create a new one
                    }
                }

                if (appointment == null)
                {
                    appointment = (Outlook.AppointmentItem)outlookApp.CreateItem(Outlook.OlItemType.olAppointmentItem);
                }

                appointment.Subject = $"To-Do: {title}";
                appointment.Body = notes ?? string.Empty;
                appointment.Start = dueDate.Value;
                if (dueDate.Value.TimeOfDay == TimeSpan.Zero)
                {
                    appointment.End = dueDate.Value.Date.AddDays(1);
                    appointment.AllDayEvent = true;
                    appointment.ReminderMinutesBeforeStart = 0;
                }
                else
                {
                    appointment.End = dueDate.Value.AddMinutes(30);
                    appointment.AllDayEvent = false;
                    appointment.ReminderMinutesBeforeStart = 15;
                }
                appointment.ReminderSet = true;

                appointment.Save();

                return appointment.EntryID;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Outlook sync failed: {ex.Message}");
                return existingEventId;
            }
            finally
            {
                if (appointment != null) Marshal.ReleaseComObject(appointment);
                if (ns != null) Marshal.ReleaseComObject(ns);
                if (outlookApp != null) Marshal.ReleaseComObject(outlookApp);
            }
        }

        public void DeleteEvent(string? eventId)
        {
            if (_localSettings.Settings.DisableOutlookSync || string.IsNullOrEmpty(eventId)) return;

            Outlook.Application? outlookApp = null;
            Outlook.NameSpace? ns = null;
            Outlook.AppointmentItem? appointment = null;
            try
            {
                outlookApp = GetOutlookApplication();
                if (outlookApp == null) return;

                ns = outlookApp.GetNamespace("MAPI");
                appointment = (Outlook.AppointmentItem)ns.GetItemFromID(eventId);
                if (appointment != null)
                {
                    appointment.Delete();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete Outlook event: {ex.Message}");
            }
            finally
            {
                if (appointment != null) Marshal.ReleaseComObject(appointment);
                if (ns != null) Marshal.ReleaseComObject(ns);
                if (outlookApp != null) Marshal.ReleaseComObject(outlookApp);
            }
        }

        public List<CalendarEvent> GetOutlookEvents(DateTime start, DateTime end)
        {
            var list = new List<CalendarEvent>();
            if (_localSettings.Settings.DisableOutlookSync) return list;

            Outlook.Application? outlookApp = null;
            Outlook.NameSpace? ns = null;
            Outlook.MAPIFolder? calendarFolder = null;
            Outlook.Items? items = null;
            Outlook.Items? filteredItems = null;

            try
            {
                outlookApp = GetOutlookApplication();
                if (outlookApp == null) return list;

                ns = outlookApp.GetNamespace("MAPI");
                calendarFolder = ns.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderCalendar);
                items = calendarFolder.Items;
                items.IncludeRecurrences = true;
                items.Sort("[Start]", false);

                // Format filter string for Outlook items query
                // Dates should be in format "MM/dd/yyyy HH:mm tt"
                string filter = $"[Start] >= '{start:g}' AND [End] <= '{end:g}'";
                filteredItems = items.Restrict(filter);

                foreach (object item in filteredItems)
                {
                    if (item is Outlook.AppointmentItem appt)
                    {
                        list.Add(new CalendarEvent
                        {
                            Id = Guid.NewGuid(),
                            Type = CalendarEventType.Meeting,
                            Title = appt.Subject ?? "Outlook Event",
                            Description = appt.Body ?? string.Empty,
                            StartDate = appt.Start,
                            EndDate = appt.End,
                            Color = "#0078D4", // Outlook Blue
                            OriginalSource = null
                        });
                        Marshal.ReleaseComObject(appt);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving Outlook calendar items: {ex.Message}");
            }
            finally
            {
                if (filteredItems != null) Marshal.ReleaseComObject(filteredItems);
                if (items != null) Marshal.ReleaseComObject(items);
                if (calendarFolder != null) Marshal.ReleaseComObject(calendarFolder);
                if (ns != null) Marshal.ReleaseComObject(ns);
                if (outlookApp != null) Marshal.ReleaseComObject(outlookApp);
            }

            return list;
        }

        public void CheckUpcomingMeetings(Action<string, DateTime> onMeetingStartingSoon)
        {
            if (_localSettings.Settings.DisableOutlookSync || _localSettings.Settings.MuteOutlookReminders) return;

            Outlook.Application? outlookApp = null;
            Outlook.NameSpace? ns = null;
            Outlook.MAPIFolder? calendarFolder = null;
            Outlook.Items? items = null;
            Outlook.Items? filteredItems = null;

            try
            {
                outlookApp = GetOutlookApplication();
                if (outlookApp == null) return;

                ns = outlookApp.GetNamespace("MAPI");
                calendarFolder = ns.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderCalendar);
                items = calendarFolder.Items;
                items.IncludeRecurrences = true;
                items.Sort("[Start]", false);

                // Scan meetings starting in the next 15 minutes
                DateTime now = DateTime.Now;
                DateTime checkWindowEnd = now.AddMinutes(15);

                string filter = $"[Start] >= '{now:g}' AND [Start] <= '{checkWindowEnd:g}'";
                filteredItems = items.Restrict(filter);

                foreach (object item in filteredItems)
                {
                    if (item is Outlook.AppointmentItem appt)
                    {
                        string entryId = appt.EntryID;
                        if (!_notifiedEventIds.Contains(entryId))
                        {
                            _notifiedEventIds.Add(entryId);
                            onMeetingStartingSoon(appt.Subject ?? "Upcoming Meeting", appt.Start);
                        }
                        Marshal.ReleaseComObject(appt);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning upcoming Outlook meetings: {ex.Message}");
            }
            finally
            {
                if (filteredItems != null) Marshal.ReleaseComObject(filteredItems);
                if (items != null) Marshal.ReleaseComObject(items);
                if (calendarFolder != null) Marshal.ReleaseComObject(calendarFolder);
                if (ns != null) Marshal.ReleaseComObject(ns);
                if (outlookApp != null) Marshal.ReleaseComObject(outlookApp);
            }
        }
    }
}
