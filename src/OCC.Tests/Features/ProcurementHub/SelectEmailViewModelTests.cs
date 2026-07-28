using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OCC.Tests.Features.ProcurementHub
{
    public class SelectEmailViewModelTests
    {
        [Fact]
        public void SelectEmailViewModel_InitializesWithParsedOptions_AndFormatsResultOnConfirm()
        {
            // Arrange
            var availableEmails = new List<string> { "contact1@supplier.com", "contact2@supplier.com" };
            var vm = new SelectEmailViewModel("Test Supplier", availableEmails);

            // Assert initialization
            Assert.Equal("Test Supplier", vm.SupplierName);
            Assert.Equal(2, vm.EmailOptions.Count);

            string? selectedResult = null;
            vm.Completed += (result) => selectedResult = result;

            // Act - Confirm default selection (all checked)
            vm.ConfirmCommand.Execute(null);

            // Assert
            Assert.Equal("contact1@supplier.com; contact2@supplier.com", selectedResult);
        }

        [Fact]
        public void SelectEmailViewModel_WithCustomEmail_AppendsCustomEmailToResult()
        {
            // Arrange
            var availableEmails = new List<string> { "main@supplier.com" };
            var vm = new SelectEmailViewModel("Test Supplier", availableEmails);
            vm.CustomEmail = "custom@supplier.com";

            string? selectedResult = null;
            vm.Completed += (result) => selectedResult = result;

            // Act
            vm.ConfirmCommand.Execute(null);

            // Assert
            Assert.Equal("main@supplier.com; custom@supplier.com", selectedResult);
        }

        [Fact]
        public void SelectEmailViewModel_AddContactCommand_RaisesAddContactRequested_AndAppendsOption()
        {
            // Arrange
            var availableEmails = new List<string> { "main@supplier.com" };
            var vm = new SelectEmailViewModel("Test Supplier", availableEmails);

            vm.AddContactRequested += (callback) =>
            {
                callback(new SupplierContact
                {
                    ContactName = "Jane Doe",
                    Email = "jane@supplier.com",
                    Phone = "0123456789",
                    Department = "Sales"
                });
            };

            // Act
            vm.AddContactCommand.Execute(null);

            // Assert
            Assert.Equal(2, vm.EmailOptions.Count);
            Assert.Contains(vm.EmailOptions, o => o.Email == "jane@supplier.com" && o.IsSelected);
        }

        [Fact]
        public void AddSupplierContactViewModel_ValidInput_CreatesSupplierContactInstance()
        {
            // Arrange
            var vm = new AddSupplierContactViewModel("Acme Supplies");
            vm.ContactName = "John Doe";
            vm.Email = "john@acme.com";
            vm.Phone = "0112345678";
            vm.Department = "Accounts";

            SupplierContact? createdContact = null;
            vm.Completed += (c) => createdContact = c;

            // Act
            vm.ConfirmCommand.Execute(null);

            // Assert
            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(createdContact);
            Assert.Equal("John Doe", createdContact!.ContactName);
            Assert.Equal("john@acme.com", createdContact.Email);
            Assert.Equal("0112345678", createdContact.Phone);
            Assert.Equal("Accounts", createdContact.Department);
        }

        [Fact]
        public void AddSupplierContactViewModel_InvalidEmail_SetsErrorMessageAndDoesNotInvokeCompleted()
        {
            // Arrange
            var vm = new AddSupplierContactViewModel("Acme Supplies");
            vm.ContactName = "John Doe";
            vm.Email = "invalid-email-address";

            SupplierContact? createdContact = null;
            vm.Completed += (c) => createdContact = c;

            // Act
            vm.ConfirmCommand.Execute(null);

            // Assert
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal("Please enter a valid email address.", vm.ErrorMessage);
            Assert.Null(createdContact);
        }
    }
}
