using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PersonalTodosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PersonalTodosController(AppDbContext context)
        {
            _context = context;
        }

        private Guid? GetCurrentUserId()
        {
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idStr, out var id))
            {
                return id;
            }
            return null;
        }

        // GET: api/PersonalTodos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PersonalTodoDto>>> GetTodos()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var todos = await _context.PersonalTodos
                .Where(t => t.UserId == userId.Value && t.IsActive)
                .OrderBy(t => t.IsComplete)
                .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(t => t.CreatedAtUtc)
                .Select(t => new PersonalTodoDto
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    Title = t.Title,
                    Notes = t.Notes,
                    DueDate = t.DueDate,
                    IsComplete = t.IsComplete,
                    CompletedAtUtc = t.CompletedAtUtc,
                    OutlookEventId = t.OutlookEventId,
                    CreatedAtUtc = t.CreatedAtUtc
                })
                .ToListAsync();

            return todos;
        }

        // GET: api/PersonalTodos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PersonalTodoDto>> GetTodo(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var todo = await _context.PersonalTodos
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId.Value && t.IsActive);

            if (todo == null) return NotFound();

            return new PersonalTodoDto
            {
                Id = todo.Id,
                UserId = todo.UserId,
                Title = todo.Title,
                Notes = todo.Notes,
                DueDate = todo.DueDate,
                IsComplete = todo.IsComplete,
                CompletedAtUtc = todo.CompletedAtUtc,
                OutlookEventId = todo.OutlookEventId,
                CreatedAtUtc = todo.CreatedAtUtc
            };
        }

        // POST: api/PersonalTodos
        [HttpPost]
        public async Task<ActionResult<PersonalTodoDto>> CreateTodo([FromBody] CreatePersonalTodoDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "User";

            var todo = new PersonalTodo
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Title = dto.Title,
                Notes = dto.Notes,
                DueDate = dto.DueDate,
                IsComplete = false,
                OutlookEventId = dto.OutlookEventId,
                CreatedBy = username
            };

            _context.PersonalTodos.Add(todo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTodo), new { id = todo.Id }, new PersonalTodoDto
            {
                Id = todo.Id,
                UserId = todo.UserId,
                Title = todo.Title,
                Notes = todo.Notes,
                DueDate = todo.DueDate,
                IsComplete = todo.IsComplete,
                CompletedAtUtc = todo.CompletedAtUtc,
                OutlookEventId = todo.OutlookEventId,
                CreatedAtUtc = todo.CreatedAtUtc
            });
        }

        // PUT: api/PersonalTodos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTodo(Guid id, [FromBody] UpdatePersonalTodoDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var todo = await _context.PersonalTodos
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId.Value && t.IsActive);

            if (todo == null) return NotFound();

            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "User";

            todo.Title = dto.Title;
            todo.Notes = dto.Notes;
            todo.DueDate = dto.DueDate;
            todo.OutlookEventId = dto.OutlookEventId;
            todo.UpdatedBy = username;
            todo.UpdatedAtUtc = DateTime.UtcNow;

            if (dto.IsComplete && !todo.IsComplete)
            {
                todo.IsComplete = true;
                todo.CompletedAtUtc = DateTime.UtcNow;
            }
            else if (!dto.IsComplete && todo.IsComplete)
            {
                todo.IsComplete = false;
                todo.CompletedAtUtc = null;
            }

            _context.Entry(todo).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TodoExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // DELETE: api/PersonalTodos/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTodo(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var todo = await _context.PersonalTodos
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId.Value && t.IsActive);

            if (todo == null) return NotFound();

            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "User";

            // Use soft delete
            todo.IsActive = false;
            todo.UpdatedBy = username;
            todo.UpdatedAtUtc = DateTime.UtcNow;

            _context.Entry(todo).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TodoExists(Guid id)
        {
            return _context.PersonalTodos.Any(e => e.Id == id && e.IsActive);
        }
    }
}
