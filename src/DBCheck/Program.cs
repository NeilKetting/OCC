using System;
using Microsoft.Data.Sqlite;

public class Program
{
    public static void Main()
    {
        using var connection = new SqliteConnection("Data Source=../OCC.API/occ.db");
        connection.Open();

        Console.WriteLine("=== EMPLOYEES ===");
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FirstName, LastName, Branch, ShiftStartTime, ShiftEndTime, IsBibc, LivesInCompanyHousing FROM Employees WHERE LastName LIKE '%Fester%' OR FirstName LIKE '%Xavier%'";
        string xavierId = "";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                xavierId = reader["Id"].ToString();
                Console.WriteLine($"Emp: Id={xavierId}, Name={reader["FirstName"]} {reader["LastName"]}, Branch={reader["Branch"]}, Shift={reader["ShiftStartTime"]}-{reader["ShiftEndTime"]}, IsBibc={reader["IsBibc"]}, Housing={reader["LivesInCompanyHousing"]}");
            }
        }

        if (string.IsNullOrEmpty(xavierId))
        {
            Console.WriteLine("Xavier Fester not found!");
            return;
        }

        Console.WriteLine("\n=== ATTENDANCE RECORDS ===");
        command.CommandText = $"SELECT Id, Date, Status, CheckInTime, CheckOutTime, NormalHours, PaidLeaveHours, StatusRemarks FROM AttendanceRecords WHERE EmployeeId = '{xavierId}' ORDER BY Date DESC";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                Console.WriteLine($"Attendance: Date={reader["Date"]}, Status={reader["Status"]}, In={reader["CheckInTime"]}, Out={reader["CheckOutTime"]}, Hrs={reader["NormalHours"]}, PaidLeaveHrs={reader["PaidLeaveHours"]}, Remarks={reader["StatusRemarks"]}");
            }
        }

        Console.WriteLine("\n=== WAGE RUN LINES ===");
        command.CommandText = $"SELECT * FROM WageRunLines WHERE EmployeeId = '{xavierId}'";
        using (var reader = command.ExecuteReader())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                Console.Write(reader.GetName(i) + "\t");
            }
            Console.WriteLine();
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    Console.Write(reader.GetValue(i) + "\t");
                }
                Console.WriteLine();
            }
        }
        
        Console.WriteLine("\n=== WAGE RUNS ===");
        command.CommandText = $"SELECT Id, StartDate, EndDate, Status, Branch, PayType FROM WageRuns";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                Console.WriteLine($"WageRun: Id={reader["Id"]}, Start={reader["StartDate"]}, End={reader["EndDate"]}, Status={reader["Status"]}, Branch={reader["Branch"]}, PayType={reader["PayType"]}");
            }
        }
    }
}
