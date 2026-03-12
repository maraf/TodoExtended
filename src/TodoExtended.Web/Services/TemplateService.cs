using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class TemplateService(IDbContextFactory<AppDbContext> dbContextFactory, ITodoService todoService, IUserTimeZoneService userTimeZoneService) : ITemplateService
{
    public async Task<IReadOnlyList<TaskTemplate>> GetAllAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.TaskTemplates
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<TaskTemplate?> GetByIdAsync(Guid id, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.TaskTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    }

    public async Task<TaskTemplate> CreateAsync(TaskTemplate template, string userId)
    {
        template.UserId = userId;
        await using var db = await dbContextFactory.CreateDbContextAsync();
        db.TaskTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    public async Task UpdateAsync(TaskTemplate template, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var existing = await db.TaskTemplates
            .FirstOrDefaultAsync(t => t.Id == template.Id && t.UserId == userId)
            ?? throw new InvalidOperationException($"Template with ID {template.Id} not found or does not belong to user.");

        existing.Title = template.Title;
        existing.TaskListId = template.TaskListId;
        existing.TaskListName = template.TaskListName;
        existing.DueDateToday = template.DueDateToday;
        existing.ReminderTime = template.ReminderTime;
        existing.SortOrder = template.SortOrder;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var template = await db.TaskTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (template is not null)
        {
            db.TaskTemplates.Remove(template);
            await db.SaveChangesAsync();
        }
    }

    public async Task<TodoTask> ExecuteTemplateAsync(Guid templateId, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var template = await db.TaskTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.UserId == userId)
            ?? throw new InvalidOperationException($"Template with ID {templateId} not found or does not belong to user.");

        DateOnly? dueDate = template.DueDateToday
            ? await userTimeZoneService.GetTodayAsync()
            : null;

        return await todoService.CreateTaskAsync(template.TaskListId, template.Title, dueDate, template.ReminderTime);
    }
}
