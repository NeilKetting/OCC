using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using Xunit;

namespace OCC.Tests.Features.ProcurementHub
{
    public class ScopeOfWorkHistoryTests
    {
        private class DummyToastService : IToastService
        {
            public void ShowInfo(string title, string message, bool autoClose = true) { }
            public void ShowSuccess(string title, string message, bool autoClose = true) { }
            public void ShowWarning(string title, string message, bool autoClose = true) { }
            public void ShowError(string title, string message, bool autoClose = true) { }
        }

        [Fact]
        public void AddAndRemoveScopeOfWorkHistory_SavesAndFiltersCorrectly()
        {
            var service = new LocalSettingsService(NullLogger<LocalSettingsService>.Instance, new DummyToastService());

            service.AddScopeOfWorkHistory("Piping & Plumbing Installation");
            service.AddScopeOfWorkHistory("Electrical Rewiring Phase 1");

            Assert.Contains("Piping & Plumbing Installation", service.Settings.ScopeOfWorkHistory);
            Assert.Contains("Electrical Rewiring Phase 1", service.Settings.ScopeOfWorkHistory);

            service.RemoveScopeOfWorkHistory("Piping & Plumbing Installation");
            Assert.DoesNotContain("Piping & Plumbing Installation", service.Settings.ScopeOfWorkHistory);
        }
    }
}
