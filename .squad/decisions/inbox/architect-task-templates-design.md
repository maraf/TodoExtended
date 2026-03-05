# Task Templates — Technical Design

**Date:** 2025-07-19
**Author:** Architect
**Status:** Proposed
**Feature:** Users can define task templates and create Microsoft To Do tasks from them with one click.

---

## Overview

Task Templates let users pre-configure recurring task patterns (title, target list, optional "due today") and create tasks from them instantly on the Home page. Templates are stored locally in SQLite via EF Core; task creation flows through the existing `ITodoService` → Graph API path.

---

## 1. Data Model

### Entity: `TaskTemplate`

**Namespace:** `TodoExtended.Web.Data`

```csharp
public class TaskTemplate
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string TaskListId { get; set; }
    public required string TaskListName { get; set; }
    public bool DueDateToday { get; set; }
    public int SortOrder { get; set; }
}
```

**Notes:**
- `TaskListId` is the Microsoft Graph task list ID (string, not a FK — it lives in Microsoft To Do, not our DB).
- `TaskListName` is cached display name so we don't need a Graph call just to show the button label. It may go stale if the user renames a list in To Do — acceptable tradeoff.
- `SortOrder` controls button display order on the Home page (lowest first).
- No user ID column needed — this is a single-user local app.

---

## 2. Database: EF Core + SQLite

### DbContext

**File:** `src/TodoExtended.Web/Data/AppDbContext.cs`
**Namespace:** `TodoExtended.Web.Data`

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.TaskListId).HasMaxLength(256);
            entity.Property(e => e.TaskListName).HasMaxLength(256);
        });
    }
}
```

### Connection String

**In `appsettings.json`:**
```json
"ConnectionStrings": {
    "DefaultConnection": "Data Source=todoextended.db"
}
```

The SQLite file `todoextended.db` lives next to the running app. Add to `.gitignore`.

### DI Registration (in `Program.cs`)

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

### NuGet Packages to Add

- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design` (dev dependency for migrations tooling)

### Migration Plan

1. Backend adds the EF Core packages and `AppDbContext`.
2. Run: `dotnet ef migrations add InitialCreate --project src/TodoExtended.Web`
3. Apply at startup via `app.Services.CreateScope()` → `context.Database.Migrate()` or via `dotnet ef database update`. **Recommendation:** auto-migrate at startup for this local-only app:

```csharp
// In Program.cs, after var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
```

---

## 3. Service Layer

### ITodoService Extension — Add `CreateTaskAsync`

**File:** `src/TodoExtended.Web/Services/ITodoService.cs`

Add to the existing interface:

```csharp
Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate);
```

**Implementation in `GraphTodoService`:** Create a `Microsoft.Graph.Models.TodoTask` with Title, DueDateTime (if provided), and POST via `graphClient.Me.Todo.Lists[taskListId].Tasks.PostAsync(...)`.

### ITemplateService

**File:** `src/TodoExtended.Web/Services/ITemplateService.cs`
**Namespace:** `TodoExtended.Web.Services`

```csharp
public interface ITemplateService
{
    Task<IReadOnlyList<TaskTemplate>> GetAllAsync();
    Task<TaskTemplate?> GetByIdAsync(int id);
    Task<TaskTemplate> CreateAsync(TaskTemplate template);
    Task UpdateAsync(TaskTemplate template);
    Task DeleteAsync(int id);
    Task<TodoTask> ExecuteTemplateAsync(int templateId);
}
```

### TemplateService Implementation

**File:** `src/TodoExtended.Web/Services/TemplateService.cs`

```
Dependencies: AppDbContext, ITodoService
```

- CRUD methods are straightforward EF Core operations on `AppDbContext.TaskTemplates`.
- `ExecuteTemplateAsync(int templateId)`:
  1. Load template from DB by ID.
  2. Compute `dueDate`: if `template.DueDateToday`, use `DateOnly.FromDateTime(DateTime.Now)`; otherwise `null`.
  3. Call `ITodoService.CreateTaskAsync(template.TaskListId, template.Title, dueDate)`.
  4. Return the created `TodoTask`.

### DI Registration

```csharp
builder.Services.AddScoped<ITemplateService, TemplateService>();
```

---

## 4. Blazor Components

### 4a. Home Page — Quick-Create Buttons

**File:** `src/TodoExtended.Web/Components/Pages/Home.razor`

**Changes:**
- Add `@rendermode InteractiveServer`, `@attribute [Authorize]` (only show template buttons to authenticated users).
- Inject `ITemplateService`.
- On init, load all templates via `ITemplateService.GetAllAsync()`.
- Render a button per template (ordered by `SortOrder`). Each button shows the template title.
- On click, call `ITemplateService.ExecuteTemplateAsync(template.Id)`. Show brief success/error feedback (a toast or inline message).
- Keep the existing welcome/sign-in text for unauthenticated users via `<AuthorizeView>`.

**Wireframe:**
```
TodoExtended

[Authorized]
  Welcome, Marek!

  Quick Create:
  [ 🏋️ Morning Workout ] [ 📧 Check Email ] [ 📝 Daily Standup ]

  Manage templates →

[NotAuthorized]
  Sign in with your Microsoft account to get started.
```

### 4b. Template Management Page

**File:** `src/TodoExtended.Web/Components/Pages/Templates.razor`
**Route:** `/templates`

**Capabilities:**
- List all templates in a table/card view.
- Add new template: form with Title (text input), TaskList (dropdown populated from `ITodoService.GetTaskListsAsync()`), DueDateToday (checkbox), SortOrder (number input).
- Edit existing template (inline or modal).
- Delete template (with confirmation).
- Uses `@rendermode InteractiveServer`, `@attribute [Authorize]`.
- Follows the same loading/error/MSAL patterns as Tasks.razor and Today.razor.

### 4c. Navigation

**File:** `src/TodoExtended.Web/Components/Layout/NavMenu.razor`

Add a "Templates" link inside the `<AuthorizeView><Authorized>` block, after "My Tasks":

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="templates">
        <span class="bi bi-lightning-fill-nav-menu" aria-hidden="true"></span> Templates
    </NavLink>
</div>
```

---

## 5. Full DI / Startup Summary

In `Program.cs`, the new registrations (in order):

```csharp
// After existing service registrations:

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Template service
builder.Services.AddScoped<ITemplateService, TemplateService>();

// ... after var app = builder.Build():

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
```

---

## 6. File Inventory (new files)

| File | Owner |
|------|-------|
| `src/TodoExtended.Web/Data/AppDbContext.cs` | Backend |
| `src/TodoExtended.Web/Data/TaskTemplate.cs` | Backend |
| `src/TodoExtended.Web/Services/ITemplateService.cs` | Backend |
| `src/TodoExtended.Web/Services/TemplateService.cs` | Backend |
| `src/TodoExtended.Web/Components/Pages/Templates.razor` | Frontend |

## 7. File Inventory (modified files)

| File | Owner | Change |
|------|-------|--------|
| `src/TodoExtended.Web/Services/ITodoService.cs` | Backend | Add `CreateTaskAsync` method |
| `src/TodoExtended.Web/Services/GraphTodoService.cs` | Backend | Implement `CreateTaskAsync` |
| `src/TodoExtended.Web/TodoExtended.Web.csproj` | Backend | Add EF Core SQLite packages |
| `src/TodoExtended.Web/appsettings.json` | Backend | Add connection string |
| `src/TodoExtended.Web/Program.cs` | Backend | Add DbContext, ITemplateService DI, auto-migrate |
| `src/TodoExtended.Web/Components/Pages/Home.razor` | Frontend | Add template quick-create buttons |
| `src/TodoExtended.Web/Components/Layout/NavMenu.razor` | Frontend | Add Templates nav link |
| `.gitignore` | Backend | Add `*.db` |

---

## 8. Implementation Order

1. **Backend — Phase 1:** EF Core setup (packages, entity, DbContext, connection string, migration, auto-migrate). This unblocks everything.
2. **Backend — Phase 2:** `CreateTaskAsync` on ITodoService/GraphTodoService. `ITemplateService` + `TemplateService`. DI registration.
3. **Frontend:** Templates.razor management page, Home.razor quick-create buttons, NavMenu link.

Backend Phase 1 and Phase 2 can be one PR. Frontend can work in parallel once the interfaces (`ITemplateService`, updated `ITodoService`) are committed.

---

## 9. Risks & Notes

- **TaskListId staleness:** If a user deletes a task list in Microsoft To Do that a template references, `ExecuteTemplateAsync` will get a Graph API error. The frontend should handle this gracefully (show error, suggest editing the template).
- **TaskListName staleness:** Cached name may diverge from the actual list name. Low impact — it's cosmetic.
- **No multi-user:** This design assumes a single-user local app. If multi-user is needed later, add a `UserId` column to `TaskTemplate` and filter by authenticated user's OID.
- **SQLite for local dev is fine.** If this ever moves to a hosted scenario, swap the provider to PostgreSQL/SQL Server via configuration.
