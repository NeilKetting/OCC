using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.ViewModels.Core;
using System;
using System.Collections.ObjectModel;

namespace OCC.Client.ViewModels.Help
{
    public partial class ReleaseNotesViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<ReleaseNoteItem> _releaseNotes = new();

        public event EventHandler? CloseRequested;

        public ReleaseNotesViewModel()
        {
            LoadNotes();
        }

        private void LoadNotes()
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            var versionString = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.3.5";

            ReleaseNotes = new ObservableCollection<ReleaseNoteItem>
            {
                new ReleaseNoteItem
                {
                    Version = $"{versionString} (Current)",
                    Date = DateTime.Today.ToString("d MMMM yyyy"),
                    Description = "Inventory Pricing & UI Standardization (v1.6.X)",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Inventory Pricing - Added optional 'Unit Price (R)' to inventory items.",
                        "NEW: Dynamic Price Sync - Inventory item prices automatically sync with live Order Line prices.",
                        "NEW: UI Standardization - Replaced inline action buttons with standard '...' Flyout Menus in Training View.",
                        "NEW: Visual Indicators - Certificate icon now distinctly highlights Teal when attached to a training record.",
                        "IMPROVED: Training Layout - Refactored training record form spacing and scroll behavior, added custom Reset buttons.",
                        "FIXED: DataGrid Row Deletion - Fixed UI glitch preventing users from deleting populated rows."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.6.0",
                    Date = "14 February 2026",
                    Description = "Wage Run Precision & Bug Reporting V2",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Wage Run Logic - Fixed Fortnightly cycle (Mon-Sun) and Advance Pay (Thu-Fri) calculation.",
                        "NEW: Attendance Management - Added Right-Click 'Edit Time' context menu for dispute resolution.",
                        "NEW: Bug Reporting V2 - Auto-screenshots, Delete capability (Dev), and Status Updates (Reporters).",
                        "IMPROVED: Wage Run UI - Clearer column separation for Advance Pay and Previous Adjustments.",
                        "FIXED: Bug Screenshot - Resolved issue where screenshots were missing from reports."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.5.0",
                    Date = "15 January 2026",
                    Description = "Annual Leave Automation & Security Refinement",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Automatic Leave Accrual - Implemented 1:17 accrual ratio (1 day for 17 days worked) based on BCEA.",
                        "NEW: Leave Balance UI - Dynamic 'Current Balance' display with interactive formula explanation.",
                        "IMPROVED: Leave Validation - Real-time calculation prevents over-requesting and automatically flags unpaid leave.",
                        "IMPROVED: Security Model - Transitioned from explicit 'Dev' roles to implicit email-based developer privileges.",
                        "FIXED: Bug Report Visibility - Restricted 'Closed' bug reports to developer-only visibility."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.4.3",
                    Date = "15 January 2026",
                    Description = "Task Management & System UX Refresh",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Personalized 'My Tasks' View - Home dashboard now filters to focus on your specific assignments.",
                        "NEW: Global Shortcuts - Added Escape key support to instantly close all overlays and side-drawers.",
                        "IMPROVED: Task Detail Engine - Fixed clipping, added multi-line titles, and instant subtask loading.",
                        "IMPROVED: Procurement UX - Refactored Order entry grids to standard DataGrid for 2x faster performance.",
                        "IMPROVED: Site Manager UI - Searchable assignment dropdown and visual site manager avatars.",
                        "IMPROVED: User Management - Linked Employees to User accounts for integrated permissions.",
                        "FIXED: Database Integrity - Implemented server-side persistence auditing for all entity changes.",
                        "FIXED: Task Hierarchy - Resolved 500 errors and preserved full hierarchy when opening details."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.4.0",
                    Date = "14 January 2026",
                    Description = "Health & Safety, Dark Mode & Logistics",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Health & Safety Module - Added Dashboard, Performance Monitoring, Incidents and Audits.",
                        "NEW: HSEQ Metrics - Real-time tracking for Safe Hours, Near Misses, and Audit Scores.",
                        "NEW: Dark Mode - Introduced system-wide high-contrast Dark Mode theme.",
                        "IMPROVED: Project Navigation - Fixed tab focus and default 'Projects' view loading.",
                        "IMPROVED: Order Logistics - Fixed navigation for 'Re-stock Now' and PO generation flows.",
                        "FIXED: Data Overlap - Resolved issue where order items appeared out of sequence in lists."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.3.5",
                    Date = "14 January 2026",
                    Description = "Low Stock Tracking & Codebase Health",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Low Stock Tracking - Toggle tracking for individual inventory items.",
                        "NEW: Conditional Visibility - Reorder Point is now hidden when tracking is disabled.",
                        "IMPROVED: Code Quality - Achieved zero-warning build across all projects (Client, API, Shared).",
                        "FIXED: Database Stability - Configured explicit decimal precision for all currency and rate fields.",
                        "FIXED: Orders Module Navigation - Resolved binding issues and missing menu properties.",
                        "FIXED: Order Printing - Added missing Email and Print commands to Create Order view.",
                        "FIXED: Order Filtering - Resolved issue where typing in SKU/Product fields didn't filter the dropdown."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.3.4",
                    Date = "12 January 2026",
                    Description = "Site Manager UI & Documentation",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Site Manager Selection - Searchable dropdown for assigning managers to projects.",
                        "IMPROVED: Site Manager UI - Added visual indicators (avatars) for assignment status.",
                        "IMPROVED: Documentation - Added comprehensive XML comments to IOrderManager and OrderManager.",
                        "FIXED: Bug Reporting - Improved overlay detection for more accurate bug reports."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.3.3",
                    Date = "09 January 2026",
                    Description = "Developer Tools & Notifications",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Developer Tools - Added restricted 'Developer' menu for simulations.",
                        "NEW: Toast Notification System - Real-time alerts for system-wide broadcasts.",
                        "NEW: Birthday Simulation - Added capability to test and trigger birthday greetings.",
                        "FIXED: Notification Actions - Restored 'Approve', 'Deny', and 'GoTo' buttons."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.3.2",
                    Date = "08 January 2026",
                    Description = "Overtime Approvals & Logistics",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Overtime Approval Flow - UI for reviewing and acting on OT requests.",
                        "IMPROVED: PDF Layout - Refined printing formats for purchase orders.",
                        "FIXED: Window State - App now starts maximized by default for better visibility."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.3.1",
                    Date = "07 January 2026",
                    Description = "Order Printing & Item Management",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Order Printing - Generate and preview PDF versions of orders.",
                        "NEW: Item List Feature - Manage item master records separately from inventory.",
                        "FIXED: Memory Management - Resolved potential leak in SignalR connection handling."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.3.0",
                    Date = "06 January 2026",
                    Description = "Procurement Beta",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Procurement Module - Initial rollout of Order creation and supplier tracking.",
                        "NEW: Real-time Updates - Integrated SignalR for instant UI synchronization of arrivals.",
                        "NEW: Auto-Update Mechanism - Pre-login splash screen now handles silent updates."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.1.11",
                    Date = "04 January 2026",
                    Description = "System Security & Presence",
                    Changes = new ObservableCollection<string>
                    {
                        "NEW: Safe Deletion Logic - Prevents accidental deletion of active Teams or Employees.",
                        "NEW: User Presence - Real-time Online/Away status indicators.",
                        "NEW: Session Security - 5-minute auto-logout for inactive users.",
                        "FIXED: Midnight Vanish - Active attendance sessions no longer disappear at midnight."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.0.1",
                    Date = "01 January 2026",
                    Description = "Major UX Improvements",
                    Changes = new ObservableCollection<string>
                    {
                        "Added Search functionality to Roll Call and Attendance History views.",
                        "Implemented Searchable AutoComplete boxes for Employee selection.",
                        "Standardized alphabetical sorting across all management lists."
                    }
                },
                new ReleaseNoteItem
                {
                    Version = "v1.0.0",
                    Date = "01 January 2026",
                    Description = "Initial Beta Release",
                    Changes = new ObservableCollection<string>
                    {
                        "Core Time & Attendance Module",
                        "Employee & Project Management",
                        "User Roles and Access Control",
                        "Basic Reporting and Export functionality"
                    }
                }
            };
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public class ReleaseNoteItem
    {
        public string Version { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ObservableCollection<string> Changes { get; set; } = new();
    }
}
