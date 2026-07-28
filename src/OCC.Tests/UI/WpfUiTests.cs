using System;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace OCC.Tests.UI
{
    public class WpfUiTests : IDisposable
    {
        private Application? _app;
        private UIA3Automation? _automation;

        private string GetWpfExePath()
        {
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Search candidate paths for OCC-ERP.exe
            var candidates = new[]
            {
                Path.Combine(currentDir, "OCC-ERP.exe"),
                Path.Combine(currentDir, "..", "..", "..", "..", "OCC.WpfClient", "bin", "Debug", "net10.0-windows10.0.19041", "OCC-ERP.exe"),
                Path.Combine(currentDir, "..", "..", "..", "..", "OCC.WpfClient", "bin", "Release", "net10.0-windows10.0.19041", "OCC-ERP.exe"),
                Path.Combine(currentDir, "..", "..", "..", "..", "src", "OCC.WpfClient", "bin", "Debug", "net10.0-windows10.0.19041", "OCC-ERP.exe")
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            throw new FileNotFoundException($"Could not find compiled OCC-ERP.exe for UI automation test. Looked in: {string.Join(", ", candidates)}");
        }

        [Fact(Skip = "UI Automation test requiring interactive desktop session")]
        public void Test_WpfApp_Launches_And_Renders_AuthView_Without_Runtime_Errors()
        {
            var exePath = GetWpfExePath();
            Assert.True(File.Exists(exePath), $"Executable does not exist at {exePath}");

            _automation = new UIA3Automation();
            var psi = new System.Diagnostics.ProcessStartInfo(exePath)
            {
                WorkingDirectory = Path.GetDirectoryName(exePath)!
            };
            _app = Application.Launch(psi);

            Assert.NotNull(_app);
            Assert.False(_app.HasExited, "WPF App process exited unexpectedly upon startup.");

            // Wait for SplashView to complete and MainWindow to load
            AutomationElement? mainWindow = null;
            var retryResult = FlaUI.Core.Tools.Retry.WhileNull(() =>
            {
                var windows = _app.GetAllTopLevelWindows(_automation);
                foreach (var w in windows)
                {
                    if (w.AutomationId == "MainWindow" || w.Title.Contains("OCC") || w.Title.Contains("Orange Circle"))
                    {
                        return w;
                    }
                }
                return null;
            }, TimeSpan.FromSeconds(15));

            mainWindow = retryResult.Result;
            Assert.NotNull(mainWindow);
            Assert.True(mainWindow.IsAvailable, "Main WPF Window is not available.");

            // Find AuthView controls (or descendants inside MainWindow) with retry
            var emailInput = FlaUI.Core.Tools.Retry.WhileNull(() => 
                mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("EmailTextBox")), 
                TimeSpan.FromSeconds(10)).Result;

            var passwordInput = FlaUI.Core.Tools.Retry.WhileNull(() => 
                mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("LoginPasswordBox")), 
                TimeSpan.FromSeconds(10)).Result;

            var loginButton = FlaUI.Core.Tools.Retry.WhileNull(() => 
                mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("LoginButton")), 
                TimeSpan.FromSeconds(10)).Result;

            Assert.NotNull(emailInput);
            Assert.NotNull(passwordInput);
            Assert.NotNull(loginButton);

            // Simulate typing and user interactions
            var emailBox = emailInput.AsTextBox();
            emailBox.Text = "admin@occ.local";

            Assert.Equal("admin@occ.local", emailBox.Text);

            // Ensure app is still healthy without runtime crash
            Assert.False(_app.HasExited, "WPF App crashed during UI interaction.");
        }

        [Fact(Skip = "UI Automation test requiring interactive desktop session")]
        public void Test_LocalLaptop_Environment_Login_And_Feature_Interactions()
        {
            var exePath = GetWpfExePath();
            Assert.True(File.Exists(exePath), $"Executable does not exist at {exePath}");

            _automation = new UIA3Automation();
            var psi = new System.Diagnostics.ProcessStartInfo(exePath)
            {
                WorkingDirectory = Path.GetDirectoryName(exePath)!
            };
            _app = Application.Launch(psi);

            Assert.NotNull(_app);
            Assert.False(_app.HasExited, "WPF App process exited unexpectedly upon startup.");

            // Wait for SplashView to complete and MainWindow to load
            AutomationElement? mainWindow = null;
            var retryResult = FlaUI.Core.Tools.Retry.WhileNull(() =>
            {
                var windows = _app.GetAllTopLevelWindows(_automation);
                foreach (var w in windows)
                {
                    if (w.AutomationId == "MainWindow" || w.Title.Contains("OCC") || w.Title.Contains("Orange Circle"))
                    {
                        return w;
                    }
                }
                return null;
            }, TimeSpan.FromSeconds(15));

            mainWindow = retryResult.Result;
            Assert.NotNull(mainWindow);

            // Select Local-Laptop Environment if selector is visible
            var envComboElement = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("EnvironmentComboBox"));
            if (envComboElement != null)
            {
                var comboBox = envComboElement.AsComboBox();
                try
                {
                    comboBox.Select("Local-Laptop");
                }
                catch
                {
                    // Fallback to index if string match differs by culture format
                    if (comboBox.Items.Length > 3)
                    {
                        comboBox.Select(3);
                    }
                }
            }

            // Find AuthView controls
            var emailInput = FlaUI.Core.Tools.Retry.WhileNull(() => 
                mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("EmailTextBox")), 
                TimeSpan.FromSeconds(10)).Result;

            var passwordInput = FlaUI.Core.Tools.Retry.WhileNull(() => 
                mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("LoginPasswordBox")), 
                TimeSpan.FromSeconds(10)).Result;

            var loginButton = FlaUI.Core.Tools.Retry.WhileNull(() => 
                mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("LoginButton")), 
                TimeSpan.FromSeconds(10)).Result;

            Assert.NotNull(emailInput);
            Assert.NotNull(passwordInput);
            Assert.NotNull(loginButton);

            // Enter admin credentials for local backend
            var emailBox = emailInput.AsTextBox();
            emailBox.Text = "neil@mdk.co.za";

            if (passwordInput.Patterns.Value.IsSupported)
            {
                passwordInput.Patterns.Value.Pattern.SetValue("pass");
            }
            else
            {
                var passBox = passwordInput.AsTextBox();
                passBox.Text = "pass";
            }

            // Click Login Button via UIA Invoke Pattern
            loginButton.AsButton().Invoke();

            // Give UI and backend API time to authenticate and transition to MainShell
            Thread.Sleep(3000);

            // Assert app process remains healthy without runtime crashes
            Assert.False(_app.HasExited, "WPF App crashed during Local-Laptop authentication.");
        [Fact]
        public void Test_Attach_To_Live_Logged_In_App_And_Interact()
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("OCC-ERP");
            if (processes.Length == 0) return; // Skip if app isn't currently running on desktop

            _automation = new UIA3Automation();
            _app = Application.Attach(processes[0].Id);
            Assert.NotNull(_app);

            var mainWindow = _app.GetMainWindow(_automation);
            Assert.NotNull(mainWindow);
            Assert.True(mainWindow.IsAvailable, "Main Window is active and accessible.");

            // Assert app process remains healthy without runtime crashes
            Assert.False(_app.HasExited, "WPF App remains active and healthy on user desktop.");
        }

        public void Dispose()
        {
            // Do not close live user process on attach test
            _automation?.Dispose();
        }
    }
}
