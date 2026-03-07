using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class TemplateService(AppDbContext db, ITodoService todoService, IUserTimeZoneService userTimeZoneService) : ITemplateService
{
    public async Task<IReadOnlyList<TaskTemplate>> GetAllAsync()
    {
        return await db.TaskTemplates
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<TaskTemplate?> GetByIdAsync(Guid id)
    {
        return await db.TaskTemplates.FindAsync(id);
    }

    public async Task<TaskTemplate> CreateAsync(TaskTemplate template)
    {
        db.TaskTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    public async Task UpdateAsync(TaskTemplate template)
    {
        db.TaskTemplates.Update(template);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var template = await db.TaskTemplates.FindAsync(id);
        if (template is not null)
        {
            db.TaskTemplates.Remove(template);
            await db.SaveChangesAsync();
        }
    }

    public async Task<TodoTask> ExecuteTemplateAsync(Guid templateId)
    {
        var template = await db.TaskTemplates.FindAsync(templateId)
            ?? throw new InvalidOperationException($"Template with ID {templateId} not found.");

        DateOnly? dueDate = template.DueDateToday
            ? await userTimeZoneService.GetTodayAsync()
            : null;

        return await todoService.CreateTaskAsync(template.TaskListId, template.Title, dueDate, template.ReminderTime);
    }
}
