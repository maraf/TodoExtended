using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public interface ITemplateService
{
    Task<IReadOnlyList<TaskTemplate>> GetAllAsync();
    Task<TaskTemplate?> GetByIdAsync(Guid id);
    Task<TaskTemplate> CreateAsync(TaskTemplate template);
    Task UpdateAsync(TaskTemplate template);
    Task DeleteAsync(Guid id);
    Task<TodoTask> ExecuteTemplateAsync(Guid templateId);
}
