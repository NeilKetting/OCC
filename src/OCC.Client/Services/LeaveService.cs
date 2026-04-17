using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public class LeaveService : ILeaveService
    {
        private readonly IRepository<LeaveRequest> _leaveRepository;
        private readonly IRepository<PublicHoliday> _holidayRepository;
        private readonly IRepository<AttendanceRecord> _attendanceRepository;
        private readonly IRepository<Employee> _employeeRepository;

        public LeaveService(
            IRepository<LeaveRequest> leaveRepository,
            IRepository<PublicHoliday> holidayRepository,
            IRepository<AttendanceRecord> attendanceRepository,
            IRepository<Employee> employeeRepository)
        {
            _leaveRepository = leaveRepository;
            _holidayRepository = holidayRepository;
            _attendanceRepository = attendanceRepository;
            _employeeRepository = employeeRepository;
        }

        public async Task<IEnumerable<PublicHoliday>> GetPublicHolidaysAsync(int year)
        {
            // Simple fetch, might want to cache or filter by year in Repo if supported
            var all = await _holidayRepository.GetAllAsync();
            return all.Where(h => h.Date.Year == year).OrderBy(h => h.Date);
        }

        public async Task<IEnumerable<LeaveRequest>> GetEmployeeRequestsAsync(Guid employeeId)
        {
            var all = await _leaveRepository.GetAllAsync();
            return all.Where(r => r.EmployeeId == employeeId).OrderByDescending(r => r.StartDate);
        }

        public async Task<IEnumerable<LeaveRequest>> GetPendingRequestsAsync()
        {
            var all = await _leaveRepository.GetAllAsync();
            return all.Where(r => r.Status == LeaveStatus.Pending).OrderBy(r => r.StartDate);
        }

        public async Task<IEnumerable<LeaveRequest>> GetApprovedRequestsForDateAsync(DateTime date)
        {
            var all = await _leaveRepository.GetAllAsync();
            // Check if date falls within Start/End (Inclusive) and Status is Approved
            return all.Where(r => 
                r.Status == LeaveStatus.Approved && 
                date.Date >= r.StartDate.Date && 
                date.Date <= r.EndDate.Date);
        }

        public async Task SubmitRequestAsync(LeaveRequest request)
        {
            // Calculate days before submitting
            request.NumberOfDays = await CalculateBusinessDaysAsync(request.StartDate, request.EndDate);
            await _leaveRepository.AddAsync(request);
        }

        public async Task ApproveRequestAsync(Guid requestId, Guid approverId)
        {
            var request = await _leaveRepository.GetByIdAsync(requestId);
            if (request != null)
            {
                request.Status = LeaveStatus.Approved;
                request.ApproverId = approverId;
                request.ActionedDate = DateTime.UtcNow;
                await _leaveRepository.UpdateAsync(request);
            }
        }

        public async Task RejectRequestAsync(Guid requestId, Guid approverId, string reason)
        {
            var request = await _leaveRepository.GetByIdAsync(requestId);
            if (request != null)
            {
                request.Status = LeaveStatus.Rejected;
                request.ApproverId = approverId;
                request.ActionedDate = DateTime.UtcNow;
                request.AdminComment = reason;
                await _leaveRepository.UpdateAsync(request);
            }
        }

        public async Task UpdateRequestAsync(LeaveRequest request)
        {
            request.NumberOfDays = await CalculateBusinessDaysAsync(request.StartDate, request.EndDate);
            await _leaveRepository.UpdateAsync(request);
        }

        public async Task DeleteRequestAsync(Guid requestId)
        {
            await _leaveRepository.DeleteAsync(requestId);
        }


        public async Task<int> CalculateBusinessDaysAsync(DateTime start, DateTime end)
        {
            if (end < start) return 0;

            var holidays = await _holidayRepository.GetAllAsync();
            var holidayDates = holidays.Select(h => h.Date.Date).ToHashSet();

            int businessDays = 0;
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;
                if (holidayDates.Contains(date)) continue;
                
                businessDays++;
            }

            return businessDays;
        }

        public double CalculateAnnualLeaveAccrual(double daysWorked)
        {
            // BCEA: 1 day for every 17 days worked
            if (daysWorked <= 0) return 0;
            return daysWorked / 17.0;
        }

        public double CalculateSickLeaveAccrual(double daysWorked)
        {
            // BCEA First 6 months: 1 day for every 26 days worked
            if (daysWorked <= 0) return 0;
            return daysWorked / 26.0;
        }

        public async Task<double> CalculateCurrentLeaveBalanceAsync(Guid employeeId)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            if (employee == null) return 0;
            return await CalculateCurrentLeaveBalanceAsync(employee);
        }

        public async Task<double> CalculateCurrentLeaveBalanceAsync(Employee employee)
        {
            if (employee == null) return 0;


            // 1. Get Initial Balance
            double balance = employee.AnnualLeaveBalance;

            // 2. Add Accrued (1 per 17 days worked - Status: Present/Late)
            // Only count attendance records on or after Employment Date
            var startDate = employee.EmploymentDate.Date;
            if (startDate < new DateTime(1900, 1, 1)) startDate = new DateTime(1900, 1, 1);

            var attendance = await _attendanceRepository.GetAllAsync();
            var employeeAttendance = attendance.Where(a => a.EmployeeId == employee.Id && 
                                                         a.Date.Date >= startDate &&
                                                         (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)).ToList();
            
            double daysWorked = employeeAttendance.Count;
            double accrued = CalculateAnnualLeaveAccrual(daysWorked);
            balance += accrued;

            // 3. Subtract Taken (Approved Annual Leave)
            // Only count leave taken on or after Employment Date
            var requests = await _leaveRepository.GetAllAsync();
            var approvedAnnualLeave = requests.Where(r => r.EmployeeId == employee.Id && 
                                                         r.Status == LeaveStatus.Approved && 
                                                         r.LeaveType == LeaveType.Annual &&
                                                         r.StartDate.Date >= startDate).ToList();
            
            double daysTaken = approvedAnnualLeave.Sum(r => r.NumberOfDays);
            balance -= daysTaken;

            return Math.Round(balance, 2);
        }
    }
}
