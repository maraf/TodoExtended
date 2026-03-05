using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public interface ITemplateService
{
    Task<IReadOnlyList<TaskTemplate>> GetAllAsync();
    Task<TaskTemplate?> GetByIdAsync(int id);
    Task<TaskTemplate> CreateAsync(TaskTemplate template);
    Task UpdateAsync(TaskTemplate template);
    Task DeleteAsync(int id);
    Task<TodoTask> ExecuteTemplateAsync(int templateId);
}
