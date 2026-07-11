using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.DTOs;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ITodoService
    {
        Task<List<PersonalTodoDto>> GetTodosAsync();
        Task<PersonalTodoDto> GetTodoAsync(Guid id);
        Task<PersonalTodoDto> CreateTodoAsync(CreatePersonalTodoDto dto);
        Task UpdateTodoAsync(Guid id, UpdatePersonalTodoDto dto);
        Task DeleteTodoAsync(Guid id);
    }
}
