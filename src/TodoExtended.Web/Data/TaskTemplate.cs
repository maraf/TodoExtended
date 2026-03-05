namespace TodoExtended.Web.Data;

public class TaskTemplate
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string TaskListId { get; set; }
    public required string TaskListName { get; set; }
    public bool DueDateToday { get; set; }
    public int SortOrder { get; set; }
}
