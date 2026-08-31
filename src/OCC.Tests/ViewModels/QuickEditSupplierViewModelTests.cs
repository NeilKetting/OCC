using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;
using System;
using Xunit;

namespace OCC.Tests.ViewModels
{
    public class QuickEditSupplierViewModelTests
    {
        [Fact]
        public void Constructor_InitializesWithSupplierData()
        {
            // Arrange
            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Name = "FH Chamberlain (Waltloo)",
                Email = "sales@chamberlain.co.za",
                ContactPerson = "Dave Smith",
                Phone = "012 804 0000"
            };

            // Act
            var vm = new QuickEditSupplierViewModel(supplier);

            // Assert
            Assert.Equal(supplier.Id, vm.SupplierId);
            Assert.Equal("FH Chamberlain (Waltloo)", vm.SupplierName);
            Assert.Equal("sales@chamberlain.co.za", vm.Email);
            Assert.Equal("Dave Smith", vm.ContactPerson);
            Assert.Equal("012 804 0000", vm.Phone);
        }

        [Fact]
        public void Confirm_FailsValidation_WhenEmailIsEmpty()
        {
            // Arrange
            var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Test Supplier" };
            var vm = new QuickEditSupplierViewModel(supplier);
            vm.Email = "   ";
            QuickEditSupplierResult? result = null;
            vm.Completed += r => result = r;

            // Act
            vm.ConfirmCommand.Execute(null);

            // Assert
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal("Email address is required.", vm.ErrorMessage);
            Assert.Null(result);
        }

        [Fact]
        public void Confirm_FailsValidation_WhenEmailIsInvalid()
        {
            // Arrange
            var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Test Supplier" };
            var vm = new QuickEditSupplierViewModel(supplier);
            vm.Email = "invalid-email-format";
            QuickEditSupplierResult? result = null;
            vm.Completed += r => result = r;

            // Act
            vm.ConfirmCommand.Execute(null);

            // Assert
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal("Please enter a valid email address.", vm.ErrorMessage);
            Assert.Null(result);
        }

        [Fact]
        public void Confirm_TriggersCompletedEvent_WithUpdatedResult()
        {
            // Arrange
            var supplierId = Guid.NewGuid();
            var supplier = new Supplier { Id = supplierId, Name = "FH Chamberlain" };
            var vm = new QuickEditSupplierViewModel(supplier);
            vm.Email = "orders@chamberlain.co.za";
            vm.ContactPerson = "Jane Doe";
            vm.Phone = "012 555 9999";

            QuickEditSupplierResult? result = null;
            vm.Completed += r => result = r;

            // Act
            vm.ConfirmCommand.Execute(null);

            // Assert
            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(result);
            Assert.Equal(supplierId, result!.SupplierId);
            Assert.Equal("orders@chamberlain.co.za", result.Email);
            Assert.Equal("Jane Doe", result.ContactPerson);
            Assert.Equal("012 555 9999", result.Phone);
        }

        [Fact]
        public void Cancel_TriggersCompletedEvent_WithNull()
        {
            // Arrange
            var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Test Supplier" };
            var vm = new QuickEditSupplierViewModel(supplier);

            bool eventFired = false;
            QuickEditSupplierResult? result = new QuickEditSupplierResult();
            vm.Completed += r =>
            {
                eventFired = true;
                result = r;
            };

            // Act
            vm.CancelCommand.Execute(null);

            // Assert
            Assert.True(eventFired);
            Assert.Null(result);
        }
    }
}
