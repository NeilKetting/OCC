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
    }
}
