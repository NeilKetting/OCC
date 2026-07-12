using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.DTOs;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.TodoHub.ViewModels
{
    public partial class TodoHubViewModel : ViewModelBase
    {
        private readonly ITodoService _todoService;
        private readonly LocalSettingsService _localSettingsService;
        private readonly IToastService _toastService;

        [ObservableProperty]
        private ObservableCollection<PersonalTodoDto> _todos = new();

        [ObservableProperty]
        private ObservableCollection<PersonalTodoDto> _filteredTodos = new();

        [ObservableProperty]
        private string _newTitle = string.Empty;

        [ObservableProperty]
        private string _newNotes = string.Empty;

        [ObservableProperty]
        private DateTime _newDueDate = DateTime.Today;

        [ObservableProperty]
        private bool _isAllDay = true;

        [ObservableProperty]
        private string _newTime = "08:00";

        [ObservableProperty]
        private bool _showCompleted = false;

        [ObservableProperty]
        private bool _disableOutlookSync;

        [ObservableProperty]
        private bool _isAdding;

        public TodoHubViewModel(
            ITodoService todoService,
            LocalSettingsService localSettingsService,
            IToastService toastService)
        {
            _todoService = todoService;
            _localSettingsService = localSettingsService;
            _toastService = toastService;

            Title = "My Personal To-Dos";
            DisableOutlookSync = _localSettingsService.Settings.DisableOutlookSync;

            _ = LoadTodosAsync();
        }

        [RelayCommand]
        public async Task LoadTodosAsync()
        {
            IsBusy = true;
            try
            {
                var list = await _todoService.GetTodosAsync();
                Todos = new ObservableCollection<PersonalTodoDto>(list);
                ApplyFilter();
            }
            catch (Exception)
            {
                _toastService.ShowError("Error", "Failed to load to-dos.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AddTodoAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTitle))
            {
                _toastService.ShowWarning("Validation", "To-Do Title is required.");
                return;
            }

            IsAdding = true;
            try
            {
                DateTime finalDueDate = NewDueDate.Date;
                if (!IsAllDay)
                {
                    if (TimeSpan.TryParse(NewTime, out var time))
                    {
                        finalDueDate = finalDueDate.Add(time);
                    }
                    else
                    {
                        _toastService.ShowWarning("Validation", "Invalid time format. Please use HH:mm (e.g. 08:30).");
                        IsAdding = false;
                        return;
                    }
                }

                var dto = new CreatePersonalTodoDto
                {
                    Title = NewTitle,
                    Notes = string.IsNullOrWhiteSpace(NewNotes) ? null : NewNotes,
                    DueDate = finalDueDate
                };

                var created = await _todoService.CreateTodoAsync(dto);
                if (created != null)
                {
                    Todos.Insert(0, created);
                    NewTitle = string.Empty;
                    NewNotes = string.Empty;
                    NewDueDate = DateTime.Today;
                    ApplyFilter();
                    _toastService.ShowSuccess("Success", "To-Do created successfully.");
                }
            }
            catch (Exception)
            {
                _toastService.ShowError("Error", "Failed to create to-do.");
            }
            finally
            {
                IsAdding = false;
            }
        }

        [RelayCommand]
        private async Task ToggleCompleteAsync(PersonalTodoDto todo)
        {
            if (todo == null) return;

            try
            {
                var updateDto = new UpdatePersonalTodoDto
                {
                    Title = todo.Title,
                    Notes = todo.Notes,
                    DueDate = todo.DueDate,
                    IsComplete = !todo.IsComplete,
                    OutlookEventId = todo.OutlookEventId
                };

                await _todoService.UpdateTodoAsync(todo.Id, updateDto);
                todo.IsComplete = updateDto.IsComplete;
                todo.CompletedAtUtc = todo.IsComplete ? DateTime.UtcNow : null;
                ApplyFilter();
            }
            catch (Exception)
            {
                _toastService.ShowError("Error", "Failed to update to-do.");
            }
        }

        [RelayCommand]
        private async Task DeleteTodoAsync(PersonalTodoDto todo)
        {
            if (todo == null) return;

            try
            {
                await _todoService.DeleteTodoAsync(todo.Id);
                Todos.Remove(todo);
                ApplyFilter();
                _toastService.ShowSuccess("Success", "To-Do deleted.");
            }
            catch (Exception)
            {
                _toastService.ShowError("Error", "Failed to delete to-do.");
            }
        }

        partial void OnShowCompletedChanged(bool value)
        {
            ApplyFilter();
        }

        partial void OnDisableOutlookSyncChanged(bool value)
        {
            _localSettingsService.Settings.DisableOutlookSync = value;
            _localSettingsService.Save();
        }

        private void ApplyFilter()
        {
            var filtered = Todos.AsEnumerable();
            if (!ShowCompleted)
            {
                filtered = filtered.Where(t => !t.IsComplete);
            }
            FilteredTodos = new ObservableCollection<PersonalTodoDto>(
                filtered.OrderBy(t => t.IsComplete)
                        .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            );
        }

        [RelayCommand]
        public void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }
    }
}
