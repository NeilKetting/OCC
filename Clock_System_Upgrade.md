# Clock System Upgrade Plan

## 1. The Current State & The Problem
Right now, the system uses a single `AttendanceRecords` table to handle every aspect of an employee's daily status. This includes:
- actual physical clock-ins
- physical clock-outs
- auto clock-ins
- approved leave
- sick days
- lates
- absences

Because everything lives in one bucket, the system uses the exact same fields (like a `null` CheckOutTime) to mean entirely different things. 
For example:
- A `null` CheckOutTime today means: **"The employee is currently working."** (Show on Live Board)
- A `null` CheckOutTime from yesterday means: **"The employee forgot to clock out."** (Show on Live Board as an alert)
- A `null` CheckOutTime with Status="Sick" means: **"The employee was sick."** (DO NOT show on Live Board)

This forces our queries (like `GetActiveAttendanceAsync`) to be overly complex and fragile, leading to "ghost" records appearing when the logic misses an edge case.

## 2. The Proposed Architecture
To build a robust foundation, we need to separate **Physical Access** from **Timesheet/Payroll**.

### A. Physical Clocking Logs (Immutable)
We will introduce a simple, append-only log of physical events. 
- It only cares if a person physically walked through the door (or explicitly clicked a "Clock In" button).
- It does not care if they are sick, on leave, or if it's a weekend.
- **The "Live Board" will ONLY look at this table.** If there's an "In" event without an "Out" event, they are on site. It becomes impossible to have phantom sick days on the Live Board.

### B. Timesheet Engine (Editable)
We will maintain a separate timesheet/payroll view that aggregates data.
- It will read the physical clocking logs to calculate actual hours worked.
- It will incorporate Leave and Sick records.
- **Auto Clock-Ins will become a payroll function, not a physical clocking.** Instead of inserting fake physical clock-ins into the database every morning, the Timesheet Engine will simply evaluate: *"It is a weekday and the employee is set to Auto-Pay, so credit them 8 hours for the day."*

## 3. Database Changes

### New Table: `ClockingEvents`
An immutable log of when buttons were pressed or cards swiped.
- `Id` (Guid)
- `EmployeeId` (Guid)
- `Timestamp` (DateTime)
- `EventType` (Enum: ClockIn, ClockOut)
- `Source` (String: e.g., "WebPortal", "BiometricScanner")

### Modified Table: `AttendanceRecords` (Renamed/Repurposed to `DailyTimesheet`)
This changes from being a raw log to a daily summary used for payroll.
- `Id` (Guid)
- `EmployeeId` (Guid)
- `Date` (DateOnly)
- `FirstInTime` (DateTime? - Derived from ClockingEvents)
- `LastOutTime` (DateTime? - Derived from ClockingEvents)
- `Status` (Enum: Present, Absent, Sick, Leave, AutoPaid, Late)
- `CalculatedHours` (Decimal)
- `WageEstimated` (Decimal)
- `HasMissingClockOut` (Boolean)
- `IsManualOverride` (Boolean) - True if a manager manually edited the timesheet

## 4. Execution Steps

### Phase 1: Establish the New Tables
1. Create the `ClockingEvent` entity and add it to the `AppDbContext`.
2. Generate and apply the EF Core migration.

### Phase 2: Dual Writing (The Bridge)
1. Update the frontend "Clock In / Clock Out" buttons to call a new API endpoint.
2. The new API endpoint will write an immutable record to `ClockingEvents` **AND** update the old `AttendanceRecords` table to maintain backwards compatibility while we rebuild the views.

### Phase 3: Update the Live View
1. Rewrite `TimeService.GetActiveAttendanceAsync()` to query the new `ClockingEvents` table instead of `AttendanceRecords`. 
2. Get the latest event for each employee today. If it's a `ClockIn`, they are active.

### Phase 4: Refactor Auto Clock-In & History
1. Stop the Background Service from injecting fake records into the physical logs.
2. Rewrite the `AttendanceHistoryViewModel` to calculate expected wages on the fly based on the `CompanyDetails` auto-clock-in days.
3. Migrate existing historical data.

## 5. Summary
By splitting apart *what actually happened* (swipes/clicks) from *how we pay for it* (sick/auto/leave), the entire system will become significantly less sensitive, drastically easier to query, and prepared for future integrations like physical biometric scanners.
