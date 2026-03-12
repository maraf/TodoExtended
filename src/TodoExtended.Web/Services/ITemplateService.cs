using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public interface ITemplateService
{
    Task<IReadOnlyList<TaskTemplate>> GetAllAsync(string userId);
    Task<TaskTemplate?> GetByIdAsync(Guid id, string userId);
    Task<TaskTemplate> CreateAsync(TaskTemplate template, string userId);
    Task UpdateAsync(TaskTemplate template, string userId);
    Task DeleteAsync(Guid id, string userId);
    Task<TodoTask> ExecuteTemplateAsync(Guid templateId, string userId);
}
