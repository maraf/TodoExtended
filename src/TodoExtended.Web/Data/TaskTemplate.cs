namespace TodoExtended.Web.Data;

public class TaskTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string TaskListId { get; set; }
    public required string TaskListName { get; set; }
    public bool DueDateToday { get; set; }
    public TimeOnly? ReminderTime { get; set; }
    public int SortOrder { get; set; }
    public required string UserId { get; set; }
    public User? User { get; set; }
}
