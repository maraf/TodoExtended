namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Demo mode chat service that returns canned responses and simulates the confirmation flow.
/// </summary>
public class DemoChatService : IChatService
{
    public Task<ChatResponse> SendMessageAsync(
        string userMessage,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("search") || lower.Contains("find") || lower.Contains("look for"))
        {
            if (lower.Contains("list"))
            {
                var response = new ChatResponse(
                    Text: "I found 1 matching task list:\n\n" +
                          "1. 🛒 Shopping",
                    ProposedActions: []);
                return Task.FromResult(response);
            }

            var searchResponse = new ChatResponse(
                Text: "I found 2 tasks matching your search:\n\n" +
                      "1. ⬜ Buy groceries (Shopping list)\n" +
                      "2. ⬜ Buy new headphones (Shopping list)",
                ProposedActions: []);
            return Task.FromResult(searchResponse);
        }

        if (lower.Contains("create") || lower.Contains("add") || lower.Contains("new task"))
        {
            var response = new ChatResponse(
                Text: "I can create that task for you. Please confirm the action below.",
                ProposedActions:
                [
                    new ProposedAction(
                        TaskActionType.CreateTask,
                        "Create task \"Buy groceries\"",
                        new Dictionary<string, string>
                        {
                            ["listId"] = "demo-list-personal",
                            ["title"] = "Buy groceries",
                            ["dueDate"] = DateOnly.FromDateTime(DateTime.Today).ToString("O")
                        })
                ]);
            return Task.FromResult(response);
        }

        if (lower.Contains("complete") || lower.Contains("finish") || lower.Contains("done"))
        {
            var response = new ChatResponse(
                Text: "I'll mark that task as complete. Please confirm below.",
                ProposedActions:
                [
                    new ProposedAction(
                        TaskActionType.CompleteTask,
                        "Complete task \"Buy groceries\"",
                        new Dictionary<string, string>
                        {
                            ["listId"] = "demo-list-personal",
                            ["taskId"] = "demo-task-1"
                        })
                ]);
            return Task.FromResult(response);
        }

        if (lower.Contains("template") || lower.Contains("templates"))
        {
            if (lower.Contains("create") || lower.Contains("new"))
            {
                var response = new ChatResponse(
                    Text: "I can create that template for you. Please confirm the action below.",
                    ProposedActions:
                    [
                        new ProposedAction(
                            TaskActionType.CreateTemplate,
                            "Create template \"Morning Workout\"",
                            new Dictionary<string, string>
                            {
                                ["title"] = "Morning Workout",
                                ["listId"] = "demo-list-personal",
                                ["listName"] = "🏠 Personal",
                                ["dueDateToday"] = "true",
                                ["reminderTime"] = "07:00"
                            })
                    ]);
                return Task.FromResult(response);
            }

            if (lower.Contains("delete") || lower.Contains("remove"))
            {
                var response = new ChatResponse(
                    Text: "I'll delete that template. Please confirm below.",
                    ProposedActions:
                    [
                        new ProposedAction(
                            TaskActionType.DeleteTemplate,
                            "Delete template f47ac10b-58cc-4372-a567-0e02b2c3d479",
                            new Dictionary<string, string>
                            {
                                ["templateId"] = "f47ac10b-58cc-4372-a567-0e02b2c3d479"
                            })
                    ]);
                return Task.FromResult(response);
            }

            if (lower.Contains("execute") || lower.Contains("run") || lower.Contains("use"))
            {
                var response = new ChatResponse(
                    Text: "I'll execute that template to create a task. Please confirm below.",
                    ProposedActions:
                    [
                        new ProposedAction(
                            TaskActionType.ExecuteTemplate,
                            "Execute template f47ac10b-58cc-4372-a567-0e02b2c3d479",
                            new Dictionary<string, string>
                            {
                                ["templateId"] = "f47ac10b-58cc-4372-a567-0e02b2c3d479"
                            })
                    ]);
                return Task.FromResult(response);
            }

            var listResponse = new ChatResponse(
                Text: "You have 3 templates:\n\n" +
                      "1. 🏃 Morning Workout (Personal list)\n" +
                      "2. 🛒 Weekly Shopping (Shopping list)\n" +
                      "3. 📧 Check Email (Work list)",
                ProposedActions: []);
            return Task.FromResult(listResponse);
        }

        if (lower.Contains("today") || lower.Contains("due"))
        {
            var response = new ChatResponse(
                Text: "Here are your tasks for today:\n\n" +
                      "1. ✅ Buy groceries (Shopping list)\n" +
                      "2. ⬜ Walk the dog (Personal list)\n" +
                      "3. ⬜ Review PR #42 (Work list)",
                ProposedActions: []);
            return Task.FromResult(response);
        }

        if (lower.Contains("list"))
        {
            var response = new ChatResponse(
                Text: "You have 3 task lists:\n\n" +
                      "1. 🏠 Personal — 5 tasks\n" +
                      "2. 📋 Work — 8 tasks\n" +
                      "3. 📚 Learning — 3 tasks",
                ProposedActions: [],
                TaskListReferences:
                [
                    new TaskListReference("demo-list-personal", "🏠 Personal"),
                    new TaskListReference("demo-list-work", "📋 Work"),
                    new TaskListReference("demo-list-learning", "📚 Learning"),
                ]);
            return Task.FromResult(response);
        }

        var defaultResponse = new ChatResponse(
            Text: "I'm your task management assistant! I can help you:\n\n" +
                  "• **View** your task lists and tasks\n" +
                  "• **Search** tasks or lists by keyword\n" +
                  "• **Create** new tasks\n" +
                  "• **Complete** or uncomplete tasks\n" +
                  "• Show **today's** tasks\n" +
                  "• **View** templates\n" +
                  "• **Create**, **update**, **delete**, or **execute** templates\n\n" +
                  "What would you like to do?",
            ProposedActions: []);
        return Task.FromResult(defaultResponse);
    }

    public Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(
        IReadOnlyList<ProposedAction> actions,
        IReadOnlyList<ActionConfirmation> confirmations,
        CancellationToken ct = default)
    {
        var results = new List<ActionResult>();

        foreach (var confirmation in confirmations.Where(c => c.Approved))
        {
            if (confirmation.ActionIndex >= 0 && confirmation.ActionIndex < actions.Count)
            {
                var action = actions[confirmation.ActionIndex];
                results.Add(new ActionResult(
                    confirmation.ActionIndex,
                    true,
                    $"{action.Type} completed successfully (demo)."));
            }
        }

        return Task.FromResult<IReadOnlyList<ActionResult>>(results);
    }
}
