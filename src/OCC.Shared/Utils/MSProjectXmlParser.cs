using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Shared.Utils
{
    public class MSProjectXmlParser
    {
        public async Task<(List<ProjectTask> Tasks, List<string> Resources, string? ProjectName)> ParseAsync(Stream stream, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report("Loading XML...");
                using var reader = new StreamReader(stream);
                var xmlContent = await reader.ReadToEndAsync();
                var doc = XDocument.Parse(xmlContent);

                var project = doc.Root;

                if (project == null || project.Name.LocalName != "Project")
                {
                    throw new InvalidOperationException("Invalid XML format: Root element must be 'Project'.");
                }

                // Namespace handling - MSP XML usually has a default namespace
                XNamespace ns = project.GetDefaultNamespace();

                var tasksElement = project.Element(ns + "Tasks");
                if (tasksElement == null)
                {
                    return (new List<ProjectTask>(), new List<string>(), null); // No tasks found
                }

                var flatTasks = new List<(ProjectTask Task, int OutlineLevel)>();
                var elements = tasksElement.Elements(ns + "Task").ToList();
                int total = elements.Count;
                int count = 0;

                progress?.Report($"Found {total} tasks. Parsing...");

                // Parse Resources
                var resourceMap = new Dictionary<string, string>(); // ResUID -> Name
                var allResourceNames = new HashSet<string>();
                var resourcesElement = project.Element(ns + "Resources");
                if (resourcesElement != null)
                {
                    foreach (var resElem in resourcesElement.Elements(ns + "Resource"))
                    {
                        var resUid = resElem.Element(ns + "UID")?.Value;
                        var resName = resElem.Element(ns + "Name")?.Value;
                        if (!string.IsNullOrEmpty(resUid) && !string.IsNullOrEmpty(resName))
                        {
                            resourceMap[resUid] = resName;
                            allResourceNames.Add(resName);
                        }
                    }
                }

                // Parse Assignments
                var taskResourceMap = new Dictionary<string, List<string>>(); // TaskUID -> List<ResourceName>
                var assignmentsElement = project.Element(ns + "Assignments");
                if (assignmentsElement != null)
                {
                    foreach (var assignElem in assignmentsElement.Elements(ns + "Assignment"))
                    {
                        var taskUid = assignElem.Element(ns + "TaskUID")?.Value;
                        var resUid = assignElem.Element(ns + "ResourceUID")?.Value;
                        
                        if (!string.IsNullOrEmpty(taskUid) && !string.IsNullOrEmpty(resUid) && resourceMap.TryGetValue(resUid, out var rName))
                        {
                            if (!taskResourceMap.ContainsKey(taskUid))
                                taskResourceMap[taskUid] = new List<string>();
                            taskResourceMap[taskUid].Add(rName);
                        }
                    }
                }

                // Map XML UIDs to new GUIDs for internal linking
                var uidMap = new Dictionary<string, string>();
                // Store predecessors temporarily for later resolution
                var pendingLinks = new List<(ProjectTask Task, string PredecessorXmlUid, int Type)>();

                foreach (var taskElem in elements)
                {
                    count++;
                    var name = taskElem.Element(ns + "Name")?.Value ?? "Unnamed Task";
                    var xmlUid = taskElem.Element(ns + "UID")?.Value;

                    // Slow down for visibility as requested
                    await Task.Delay(10);
                    progress?.Report($"Parsing Task {count} of {total}: {name}...");

                    // Skip if "IsNull" is 1 (blank row)
                    if (taskElem.Element(ns + "IsNull")?.Value == "1")
                        continue;

                    var startStr = taskElem.Element(ns + "Start")?.Value;
                    var finishStr = taskElem.Element(ns + "Finish")?.Value;
                    var durationStr = taskElem.Element(ns + "Duration")?.Value; // e.g. PT240H0M0S
                    var percentStr = taskElem.Element(ns + "PercentComplete")?.Value;
                    var outlineLevelStr = taskElem.Element(ns + "OutlineLevel")?.Value;
                    var priorityStr = taskElem.Element(ns + "Priority")?.Value;
                    
                    var newGuid = Guid.NewGuid();
                    if (!string.IsNullOrEmpty(xmlUid))
                    {
                        uidMap[xmlUid] = newGuid.ToString();
                    }

                    DateTime.TryParse(startStr, out var start);
                    DateTime.TryParse(finishStr, out var finish);

                    // Fallback: If finish is invalid but start and duration exist, calculate finish
                    if (finish == DateTime.MinValue && start != DateTime.MinValue && !string.IsNullOrEmpty(durationStr))
                    {
                        try
                        {
                            var durationSpan = System.Xml.XmlConvert.ToTimeSpan(durationStr);
                            finish = start.Add(durationSpan);
                        }
                        catch { }
                    }

                    // Fallback: If finish is still invalid, default to Start (0 duration task)
                    if (finish == DateTime.MinValue && start != DateTime.MinValue)
                        finish = start;

                    int.TryParse(percentStr, out var percent);
                    int.TryParse(outlineLevelStr, out var level);

                    // Format duration (PT8H0M0S -> 1 day approx or keep string)
                    string durationDisplay = FormatDuration(durationStr);
                    
                    var task = new ProjectTask
                    {
                        Id = newGuid, // Assign the new Guid
                        LegacyId = xmlUid, // Store the original XML UID as LegacyId
                        Name = name,
                        StartDate = start,
                        FinishDate = finish,
                        Duration = durationDisplay,
                        PercentComplete = percent,
                        Priority = FormatPriority(priorityStr)
                    };

                    // Populate Assignments (Names only for now, ID to be resolved by VM)
                    if (!string.IsNullOrEmpty(xmlUid) && taskResourceMap.TryGetValue(xmlUid, out var assignedNames))
                    {
                        foreach (var rName in assignedNames)
                        {
                            task.Assignments.Add(new TaskAssignment 
                            { 
                                AssigneeName = rName,
                                TaskId = task.Id
                            });
                        }
                        task.AssignedTo = string.Join(", ", assignedNames);
                    }

                    // Capture Predecessors (XML UIDs)
                    var predecessors = taskElem.Elements(ns + "PredecessorLink");
                    foreach (var predLink in predecessors)
                    {
                        var predUid = predLink.Element(ns + "PredecessorUID")?.Value;
                        var typeStr = predLink.Element(ns + "Type")?.Value; // 1=FF, 2=FS, 3=SS, 4=SF
                        
                        if (!string.IsNullOrEmpty(predUid))
                        {
                            int type = 1; 
                            if (int.TryParse(typeStr, out var t)) type = t;
                            
                            pendingLinks.Add((task, predUid, type));
                        }
                    }

                    flatTasks.Add((task, level));
                }

                // Resolve Predecessors
                foreach (var link in pendingLinks)
                {
                    if (uidMap.TryGetValue(link.PredecessorXmlUid, out var predGuid))
                    {
                        // Store as "GUID|Type"
                        link.Task.Predecessors.Add($"{predGuid}|{link.Type}");
                    }
                }

                progress?.Report("Reconstructing hierarchy...");

                // Reconstruct Hierarchy
                var rootTasks = new List<ProjectTask>();
                var levelStack = new Dictionary<int, ProjectTask>(); // Map Level -> Last Task at that Level

                foreach (var (task, level) in flatTasks)
                {
                    // Find nearest parent (level - 1 down to 0)
                    ProjectTask? parent = null;
                    for (int i = level - 1; i >= 0; i--)
                    {
                        if (levelStack.TryGetValue(i, out var p))
                        {
                            parent = p;
                            break;
                        }
                    }

                    if (parent != null)
                    {
                        parent.Children.Add(task);
                    }
                    else
                    {
                        rootTasks.Add(task);
                    }

                    // Update stack: this task is now the valid parent for lower levels
                    levelStack[level] = task;

                    // Clear deeper levels to prevent wrong parenting (e.g. moving from Level 3 back to Level 2)
                    // We must clear everything > level
                    var keysToRemove = levelStack.Keys.Where(k => k > level).ToList();
                    foreach (var k in keysToRemove) levelStack.Remove(k);
                }

                // Try to get title from Title property or fallback to filename if possible
                var projectName = project.Element(ns + "Title")?.Value ?? project.Element(ns + "Name")?.Value;

                // Final Pass: Recalculate Dates for Summaries to ensure visual consistency
                foreach (var r in rootTasks)
                {
                    RecalculateDates(r);
                }

                progress?.Report("Import complete!");
                return (rootTasks, allResourceNames.ToList(), projectName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
                return (new List<ProjectTask>(), new List<string>(), null);
            }
        }

        private void RecalculateDates(ProjectTask task)
        {
            if (task.Children == null || task.Children.Count == 0) return;

            // Recurse first
            foreach (var child in task.Children)
            {
                RecalculateDates(child);
            }

            // Calc Min/Max
            var minStart = DateTime.MaxValue;
            var maxFinish = DateTime.MinValue;
            bool hasDates = false;

            foreach (var child in task.Children)
            {
                if (child.StartDate < minStart) minStart = child.StartDate;
                if (child.FinishDate > maxFinish) maxFinish = child.FinishDate;
                hasDates = true;
            }

            if (hasDates && minStart != DateTime.MaxValue && maxFinish != DateTime.MinValue)
            {
                task.StartDate = minStart;
                task.FinishDate = maxFinish;
                
                // Force IsGroup
                task.IsGroup = true;

                // Update Duration string roughly
                var days = (maxFinish - minStart).TotalDays;
                task.Duration = $"{days:0.##} days";
            }
        }

        private string FormatDuration(string? durationStr)
        {
            if (string.IsNullOrEmpty(durationStr)) return "";
            try
            {
                var time = System.Xml.XmlConvert.ToTimeSpan(durationStr);
                double workingDays = time.TotalHours / 8.0;
                if (workingDays >= 1) return $"{workingDays:0.##} days";
                return $"{time.TotalHours:0.##} hours";
            }
            catch
            {
                return durationStr ?? "";
            }
        }

        private string FormatPriority(string? priorityStr)
        {
            if (string.IsNullOrEmpty(priorityStr)) return "Medium";
            if (int.TryParse(priorityStr, out var p))
            {
                if (p < 500) return "Low";
                if (p == 500) return "Medium";
                if (p > 500 && p < 1000) return "High";
                if (p >= 1000) return "Critical";
            }
            return priorityStr;
        }
    }
}
