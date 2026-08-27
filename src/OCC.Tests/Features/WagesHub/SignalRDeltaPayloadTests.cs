using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using Xunit;

namespace OCC.Tests.Features.WagesHub
{
    public class SignalRDeltaPayloadTests
    {
        [Fact]
        public void EntityChangeDto_EmployeePayload_ConstructsCorrectly()
        {
            var empId = Guid.NewGuid();
            var emp = new Employee
            {
                Id = empId,
                FirstName = "Jane",
                LastName = "Doe",
                EmployeeNumber = "EMP-99"
            };

            var changeDto = new EntityChangeDto<Employee>
            {
                Action = "Created",
                EntityId = empId,
                Entity = emp
            };

            Assert.Equal("Created", changeDto.Action);
            Assert.Equal(empId, changeDto.EntityId);
            Assert.Equal("Jane", changeDto.Entity.FirstName);
        }

        [Fact]
        public void EntityChangeDto_AttendanceRecordPayload_ConstructsCorrectly()
        {
            var recordId = Guid.NewGuid();
            var record = new AttendanceRecord
            {
                Id = recordId,
                EmployeeId = Guid.NewGuid(),
                Date = DateTime.Today,
                Status = AttendanceStatus.Present
            };

            var changeDto = new EntityChangeDto<AttendanceRecord>
            {
                Action = "Updated",
                EntityId = recordId,
                Entity = record
            };

            Assert.Equal("Updated", changeDto.Action);
            Assert.Equal(AttendanceStatus.Present, changeDto.Entity.Status);
        }

        [Fact]
        public void EntityChangeDto_WageSettingsPayload_ConstructsCorrectly()
        {
            var settingsId = Guid.NewGuid();
            var settings = new WageSettings
            {
                Id = settingsId,
                BibcRatePerDay = 35.00m,
                DefaultSupervisorFee = 600.00m
            };

            var changeDto = new EntityChangeDto<WageSettings>
            {
                Action = "Updated",
                EntityId = settingsId,
                Entity = settings
            };

            Assert.Equal("Updated", changeDto.Action);
            Assert.Equal(35.00m, changeDto.Entity.BibcRatePerDay);
        }

        [Fact]
        public void EntityChangeDto_ProjectSummaryPayload_ConstructsCorrectly()
        {
            var projectId = Guid.NewGuid();
            var summary = new ProjectSummaryDto
            {
                Id = projectId,
                Name = "Cape Town Warehouse Expansion",
                Status = "In Progress",
                Priority = "High"
            };

            var changeDto = new EntityChangeDto<ProjectSummaryDto>
            {
                Action = "Created",
                EntityId = projectId,
                Entity = summary
            };

            Assert.Equal("Created", changeDto.Action);
            Assert.Equal(projectId, changeDto.EntityId);
            Assert.Equal("Cape Town Warehouse Expansion", changeDto.Entity.Name);
        }

        [Fact]
        public void EntityChangeDto_CustomerSummaryPayload_ConstructsCorrectly()
        {
            var customerId = Guid.NewGuid();
            var summary = new CustomerSummaryDto
            {
                Id = customerId,
                Name = "Acme Builders",
                Email = "info@acme.co.za"
            };

            var changeDto = new EntityChangeDto<CustomerSummaryDto>
            {
                Action = "Updated",
                EntityId = customerId,
                Entity = summary
            };

            Assert.Equal("Updated", changeDto.Action);
            Assert.Equal("Acme Builders", changeDto.Entity.Name);
        }

        [Fact]
        public void EntityChangeDto_SupplierSummaryPayload_ConstructsCorrectly()
        {
            var supplierId = Guid.NewGuid();
            var summary = new SupplierSummaryDto
            {
                Id = supplierId,
                Name = "Cape Steel Suppliers",
                Email = "steel@cape.co.za"

            };

            var changeDto = new EntityChangeDto<SupplierSummaryDto>
            {
                Action = "Updated",
                EntityId = supplierId,
                Entity = summary
            };

            Assert.Equal("Updated", changeDto.Action);
            Assert.Equal("Cape Steel Suppliers", changeDto.Entity.Name);
        }

        [Fact]
        public void EntityChangeDto_SubContractorSummaryPayload_ConstructsCorrectly()
        {
            var subId = Guid.NewGuid();
            var summary = new SubContractorSummaryDto
            {
                Id = subId,
                Name = "Apex Electrical",
                Specialties = "Electrical"
            };

            var changeDto = new EntityChangeDto<SubContractorSummaryDto>
            {
                Action = "Created",
                EntityId = subId,
                Entity = summary
            };

            Assert.Equal("Created", changeDto.Action);
            Assert.Equal("Apex Electrical", changeDto.Entity.Name);
        }
    }
}

