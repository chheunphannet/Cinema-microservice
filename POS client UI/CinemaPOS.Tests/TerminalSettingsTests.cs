using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CinemaPOS.App.Forms;
using CinemaPOS.Core.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CinemaPOS.Tests
{
    public class TerminalSettingsTests
    {
        [Fact]
        public void TerminalSettingsForm_InitializesWithProperDefaults()
        {
            using var form = new TerminalSettingsForm();

            Assert.NotNull(form);
            Assert.Equal("Terminal Settings", form.Text);
            Assert.False(form.LoggedOut);
            Assert.False(form.ShiftClosed);
            Assert.Equal(FormBorderStyle.FixedDialog, form.FormBorderStyle);
            Assert.False(form.MaximizeBox);
            Assert.False(form.MinimizeBox);
        }

        [Fact]
        public void LoginForm_ClearSession_ResetsCurrentSession()
        {
            // Clear session should ensure null session state
            LoginForm.ClearSession();

            Assert.Null(LoginForm.CurrentUser);
            Assert.Null(LoginForm.CurrentShift);
        }

        [Fact]
        public void TerminalSettingsForm_ContainsAllExpectedShortcuts()
        {
            using var form = new TerminalSettingsForm();

            var expectedKeys = new[] { "F1", "F2", "F3", "F4", "F5", "F8", "F9", "F10", "F11", "F12", "Esc" };

            // Find all Labels in form
            var allLabels = form.Controls
                .Cast<Control>()
                .SelectMany(GetAllControls)
                .OfType<Label>()
                .Select(l => l.Text)
                .ToList();

            foreach (var key in expectedKeys)
            {
                Assert.Contains(allLabels, text => text == key);
            }
        }

        [Fact]
        public void TerminalSettingsForm_ContainsLogoutAndShiftButtons()
        {
            using var form = new TerminalSettingsForm();

            var allButtons = form.Controls
                .Cast<Control>()
                .SelectMany(GetAllControls)
                .OfType<Button>()
                .ToList();

            var logoutBtn = allButtons.FirstOrDefault(b => b.Text.Contains("Log Out"));
            Assert.NotNull(logoutBtn);

            var closeShiftBtn = allButtons.FirstOrDefault(b => b.Text.Contains("Close Shift"));
            Assert.NotNull(closeShiftBtn);

            var closeBtn = allButtons.FirstOrDefault(b => b.Text == "Close");
            Assert.NotNull(closeBtn);
        }

        [Fact]
        public void TerminalSettingsForm_HandlesNullUserAndShiftWithoutThrowing()
        {
            LoginForm.ClearSession();

            var exception = Record.Exception(() =>
            {
                using var form = new TerminalSettingsForm();
                form.CreateControl();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void TerminalSettingsForm_CloseButton_HasCancelDialogResult()
        {
            using var form = new TerminalSettingsForm();
            Assert.NotNull(form.CancelButton);

            var allButtons = form.Controls
                .Cast<Control>()
                .SelectMany(GetAllControls)
                .OfType<Button>()
                .ToList();

            var closeBtn = allButtons.FirstOrDefault(b => b.Text == "Close");
            Assert.NotNull(closeBtn);
            Assert.Equal(form.CancelButton, closeBtn);
        }

        [Fact]
        public void LoginForm_ResetLoginState_CanBeCalledSafely()
        {
            var config = new ConfigurationBuilder().Build();
            using var loginForm = new LoginForm(config);

            var exception = Record.Exception(() =>
            {
                loginForm.ResetLoginState();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void TerminalSettingsForm_ContainsF8Shortcut()
        {
            using var form = new TerminalSettingsForm();

            var allLabels = form.Controls
                .Cast<Control>()
                .SelectMany(GetAllControls)
                .OfType<Label>()
                .Select(l => l.Text)
                .ToList();

            Assert.Contains(allLabels, text => text == "F8");
            Assert.Contains(allLabels, text => text.Contains("Terminal Settings"));
        }

        [Fact]
        public void TerminalSettingsForm_RendersAccountInfo_WhenSessionIsActive()
        {
            var mockUser = new LoginResponse
            {
                UserId = Guid.NewGuid(),
                Username = "testcashier",
                DisplayName = "Maria Santos",
                Roles = new List<string> { "chief_cashier", "supervisor" },
                BranchId = Guid.NewGuid(),
                Token = "jwt_test_token_123"
            };

            var mockShift = new ShiftOpenResponse
            {
                ShiftId = Guid.NewGuid(),
                BranchId = mockUser.BranchId,
                CashierId = mockUser.UserId,
                TerminalCode = "POS-03",
                OpeningFloat = 250.00m,
                Status = "open",
                OpenedAt = new DateTime(2026, 9, 27, 8, 30, 0, DateTimeKind.Local)
            };

            LoginForm.SetSession(mockUser, mockShift, "Downtown Megaplex");

            try
            {
                using var form = new TerminalSettingsForm();

                var allLabels = form.Controls
                    .Cast<Control>()
                    .SelectMany(GetAllControls)
                    .OfType<Label>()
                    .Select(l => l.Text)
                    .ToList();

                Assert.Contains(allLabels, text => text == "Maria Santos");
                Assert.Contains(allLabels, text => text == "@testcashier");
                Assert.Contains(allLabels, text => text.Contains("CHIEF CASHIER"));
                Assert.Contains(allLabels, text => text == "Downtown Megaplex");
                Assert.Contains(allLabels, text => text == "POS-03");
                Assert.Contains(allLabels, t => t == "$250.00");
            }
            finally
            {
                LoginForm.ClearSession();
            }
        }

        [Fact]
        public void LoginForm_ClearSession_ClearsJwtTokenAndBranch()
        {
            var mockUser = new LoginResponse
            {
                UserId = Guid.NewGuid(),
                Username = "temp",
                DisplayName = "Temp User",
                Roles = new List<string> { "cashier" },
                BranchId = Guid.NewGuid(),
                Token = "active_jwt_token"
            };

            LoginForm.SetSession(mockUser, null, "North Branch");
            Assert.NotNull(LoginForm.CurrentUser);
            Assert.Equal("North Branch", LoginForm.CurrentBranchName);

            LoginForm.ClearSession();

            Assert.Null(LoginForm.CurrentUser);
            Assert.Null(LoginForm.CurrentShift);
            Assert.Equal("Main Cinema", LoginForm.CurrentBranchName);
        }

        [Fact]
        public void TerminalSettingsForm_EscapeKey_ClosesFormWithCancel()
        {
            using var form = new TerminalSettingsForm();
            form.Show();

            // Simulate pressing Escape
            var keyEventArgs = new KeyEventArgs(Keys.Escape);
            var onKeyDownMethod = typeof(TerminalSettingsForm).GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onKeyDownMethod?.Invoke(form, new object[] { keyEventArgs });

            Assert.Equal(DialogResult.Cancel, form.DialogResult);
            form.Close();
        }

        private static IEnumerable<Control> GetAllControls(Control parent)
        {
            yield return parent;
            foreach (Control child in parent.Controls)
            {
                foreach (Control grandChild in GetAllControls(child))
                {
                    yield return grandChild;
                }
            }
        }
    }
}
