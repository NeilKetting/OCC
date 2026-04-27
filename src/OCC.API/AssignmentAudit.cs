using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Linq;

// This is a scratch script to debug task assignments
public class AssignmentCheck
{
    public static void Run(AppDbContext context)
    {
        Console.WriteLine("--- TASK ASSIGNMENT AUDIT ---");
        
        var totalTasks = context.ProjectTasks.Count();
        var totalAssignments = context.TaskAssignments.Count();
        
        Console.WriteLine($"Total Tasks: {totalTasks}");
        Console.WriteLine($"Total Assignments: {totalAssignments}");
        
        var assignments = context.TaskAssignments.ToList();
        foreach (var a in assignments)
        {
            Console.WriteLine($"Task: {a.TaskId}, Assignee: {a.AssigneeName}, Type: {a.AssigneeType}, ID: {a.AssigneeId}");
        }
        
        var employees = context.Employees.ToList();
        Console.WriteLine("\n--- EMPLOYEES ---");
        foreach (var e in employees)
        {
            Console.WriteLine($"Employee: {e.FirstName} {e.LastName}, ID: {e.Id}, LinkedUser: {e.LinkedUserId}, Email: {e.Email}");
        }
        
        var users = context.Users.ToList();
        Console.WriteLine("\n--- USERS ---");
        foreach (var u in users)
        {
            Console.WriteLine($"User: {u.FirstName} {u.LastName}, ID: {u.Id}, Email: {u.Email}, Role: {u.UserRole}");
        }
    }
}
