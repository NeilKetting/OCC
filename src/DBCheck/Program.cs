using System;
using Microsoft.Data.Sqlite;

public class Program
{
    public static void Main()
    {
        using var connection = new SqliteConnection("Data Source=../OCC.API/occ.db");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ClockingEvents";

        using var reader = command.ExecuteReader();
        int count = 0;
        while (reader.Read())
        {
            count++;
            Console.WriteLine($"Event: Emp={reader["EmployeeId"]}, Time={reader["Timestamp"]}, Type={reader["EventType"]}");
        }
        Console.WriteLine($"Total Events: {count}");

        command.CommandText = "SELECT * FROM AttendanceRecords WHERE substr(Date, 1, 10) = date('now')";
        using var reader2 = command.ExecuteReader();
        int count2 = 0;
        while (reader2.Read())
        {
            count2++;
            Console.WriteLine($"V1: Emp={reader2["EmployeeId"]}, In={reader2["CheckInTime"]}, Out={reader2["CheckOutTime"]}");
        }
        Console.WriteLine($"Total V1 Today: {count2}");
    }
}
