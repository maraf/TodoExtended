using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Kiota.Abstractions;
using NSubstitute;
using TodoExtended.Web.Services;

using GraphTodoTask = Microsoft.Graph.Models.TodoTask;
using GraphDateTimeTimeZone = Microsoft.Graph.Models.DateTimeTimeZone;
using GraphPatternedRecurrence = Microsoft.Graph.Models.PatternedRecurrence;
using GraphRecurrencePattern = Microsoft.Graph.Models.RecurrencePattern;
using GraphRecurrenceRange = Microsoft.Graph.Models.RecurrenceRange;

namespace TodoExtended.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GraphTodoService"/>, focusing on reminder rescheduling
/// when a task's due date changes.
/// </summary>
public class GraphTodoServiceTests
{
    private const string ListId = "list-1";
    private const string TaskId = "task-1";

    /// <summary>
    /// When Graph returns the reminder in UTC and the user's timezone is UTC+1 (CET),
    /// the rescheduled reminder must fire at the same local clock time, not -1 hour.
    /// Regression test for: reminder moved -1h when task is rescheduled.
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_ReminderInUtc_PreservesUserLocalTime()
    {
        // Arrange
        // Use a fixed-offset zone to avoid platform TZ database differences in CI.
        // We simulate: user sees UTC+1 (CET), Graph stores reminder in UTC.
        var cetZone = TimeZoneInfo.CreateCustomTimeZone("TestZone+1", TimeSpan.FromHours(1), "TestZone+1", "TestZone+1");

        // Existing task: reminder is ON and Graph returned it in UTC (08:00 UTC = 09:00 CET)
        var existingTask = new GraphTodoTask
        {
            IsReminderOn = true,
            ReminderDateTime = new GraphDateTimeTimeZone
            {
                DateTime = "2026-03-15T08:00:00", // 08:00 UTC
                TimeZone = "UTC",
            },
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(cetZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        var newDueDate = new DateOnly(2026, 3, 22);

        // Act
        await service.SetTaskDueDateAsync(ListId, TaskId, newDueDate, "user-1");

        // Assert: reminder should be 09:00 in CET (the user's original local time), not 08:00
        Assert.NotNull(patchedTask);
        Assert.True(patchedTask.IsReminderOn == true);
        Assert.NotNull(patchedTask.ReminderDateTime);
        Assert.Equal("2026-03-22T09:00:00", patchedTask.ReminderDateTime.DateTime);
        Assert.Equal(cetZone.Id, patchedTask.ReminderDateTime.TimeZone);
    }

    /// <summary>
    /// When Graph returns the reminder already in the user's timezone, the rescheduled
    /// reminder must preserve the same time without any additional conversion.
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_ReminderInUserTimezone_PreservesLocalTime()
    {
        // Arrange
        var userZone = TimeZoneInfo.CreateCustomTimeZone("TestZone+1", TimeSpan.FromHours(1), "TestZone+1", "TestZone+1");

        // Graph returned reminder already in user's timezone (09:00 CET)
        var existingTask = new GraphTodoTask
        {
            IsReminderOn = true,
            ReminderDateTime = new GraphDateTimeTimeZone
            {
                DateTime = "2026-03-15T09:00:00",
                TimeZone = userZone.Id,
            },
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(userZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        var newDueDate = new DateOnly(2026, 3, 22);

        // Act
        await service.SetTaskDueDateAsync(ListId, TaskId, newDueDate, "user-1");

        // Assert: 09:00 in CET is preserved
        Assert.NotNull(patchedTask);
        Assert.True(patchedTask.IsReminderOn == true);
        Assert.NotNull(patchedTask.ReminderDateTime);
        Assert.Equal("2026-03-22T09:00:00", patchedTask.ReminderDateTime.DateTime);
        Assert.Equal(userZone.Id, patchedTask.ReminderDateTime.TimeZone);
    }

    /// <summary>
    /// When Graph returns the reminder with an unrecognized timezone ID, the code falls back
    /// to treating the datetime as already in the user's local timezone (no exception thrown).
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_ReminderWithUnknownTimezone_FallsBackToUserLocalTime()
    {
        // Arrange
        var userZone = TimeZoneInfo.CreateCustomTimeZone("TestZone+1", TimeSpan.FromHours(1), "TestZone+1", "TestZone+1");

        var existingTask = new GraphTodoTask
        {
            IsReminderOn = true,
            ReminderDateTime = new GraphDateTimeTimeZone
            {
                DateTime = "2026-03-15T09:00:00",
                TimeZone = "Unknown/Bogus_Timezone_That_Does_Not_Exist",
            },
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(userZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        var newDueDate = new DateOnly(2026, 3, 22);

        // Act — must not throw
        await service.SetTaskDueDateAsync(ListId, TaskId, newDueDate, "user-1");

        // Assert: falls back to treating the raw time as user-local (09:00)
        Assert.NotNull(patchedTask);
        Assert.True(patchedTask.IsReminderOn == true);
        Assert.NotNull(patchedTask.ReminderDateTime);
        Assert.Equal("2026-03-22T09:00:00", patchedTask.ReminderDateTime.DateTime);
        Assert.Equal(userZone.Id, patchedTask.ReminderDateTime.TimeZone);
    }

    /// <summary>
    /// When the task has no active reminder, the patch must not include a reminder.
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_NoReminder_DoesNotAddReminder()
    {
        // Arrange
        var userZone = TimeZoneInfo.Utc;

        var existingTask = new GraphTodoTask
        {
            IsReminderOn = false,
            ReminderDateTime = null,
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(userZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        // Act
        await service.SetTaskDueDateAsync(ListId, TaskId, new DateOnly(2026, 3, 22), "user-1");

        // Assert: no reminder fields set on the patch
        Assert.NotNull(patchedTask);
        Assert.Null(patchedTask.IsReminderOn);
        Assert.Null(patchedTask.ReminderDateTime);
    }

    /// <summary>
    /// When rescheduling a recurring task (with a daily recurrence and no-end range),
    /// the patch must include the recurrence with an updated startDate matching the new due date.
    /// This prevents Microsoft To Do from creating a duplicate occurrence for the original date.
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_RecurringTask_UpdatesRecurrenceStartDate()
    {
        // Arrange
        var userZone = TimeZoneInfo.Utc;
        var newDueDate = new DateOnly(2026, 4, 10);

        var existingTask = new GraphTodoTask
        {
            IsReminderOn = false,
            ReminderDateTime = null,
            Recurrence = new GraphPatternedRecurrence
            {
                Pattern = new GraphRecurrencePattern
                {
                    Type = Microsoft.Graph.Models.RecurrencePatternType.Daily,
                    Interval = 1,
                },
                Range = new GraphRecurrenceRange
                {
                    Type = Microsoft.Graph.Models.RecurrenceRangeType.NoEnd,
                    StartDate = new Date(2026, 3, 1),
                },
            },
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(userZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        // Act
        await service.SetTaskDueDateAsync(ListId, TaskId, newDueDate, "user-1");

        // Assert: recurrence is included in the patch with startDate updated to the new due date
        Assert.NotNull(patchedTask);
        Assert.NotNull(patchedTask.Recurrence);
        Assert.NotNull(patchedTask.Recurrence.Range);
        Assert.Equal(new Date(2026, 4, 10), patchedTask.Recurrence.Range.StartDate);
        Assert.Equal(Microsoft.Graph.Models.RecurrenceRangeType.NoEnd, patchedTask.Recurrence.Range.Type);
    }

    /// <summary>
    /// When rescheduling a recurring task that also has an active reminder,
    /// the patch must include both the updated recurrence startDate and the rescheduled reminder.
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_RecurringTaskWithReminder_UpdatesBothRecurrenceAndReminder()
    {
        // Arrange
        var userZone = TimeZoneInfo.Utc;
        var newDueDate = new DateOnly(2026, 4, 10);

        var existingTask = new GraphTodoTask
        {
            IsReminderOn = true,
            ReminderDateTime = new GraphDateTimeTimeZone
            {
                DateTime = "2026-03-01T09:00:00",
                TimeZone = "UTC",
            },
            Recurrence = new GraphPatternedRecurrence
            {
                Pattern = new GraphRecurrencePattern
                {
                    Type = Microsoft.Graph.Models.RecurrencePatternType.Weekly,
                    Interval = 1,
                },
                Range = new GraphRecurrenceRange
                {
                    Type = Microsoft.Graph.Models.RecurrenceRangeType.NoEnd,
                    StartDate = new Date(2026, 3, 1),
                },
            },
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(userZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        // Act
        await service.SetTaskDueDateAsync(ListId, TaskId, newDueDate, "user-1");

        // Assert: recurrence startDate is updated
        Assert.NotNull(patchedTask);
        Assert.NotNull(patchedTask.Recurrence?.Range);
        Assert.Equal(new Date(2026, 4, 10), patchedTask.Recurrence!.Range!.StartDate);
        // Assert: reminder is rescheduled to same time on the new due date
        Assert.True(patchedTask.IsReminderOn);
        Assert.Equal("2026-04-10T09:00:00", patchedTask.ReminderDateTime?.DateTime);
    }

    /// <summary>
    /// When the task is not recurring (Recurrence is null), the patch must not include a recurrence.
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_NonRecurringTask_DoesNotAddRecurrence()
    {
        // Arrange
        var userZone = TimeZoneInfo.Utc;

        var existingTask = new GraphTodoTask
        {
            IsReminderOn = false,
            ReminderDateTime = null,
            Recurrence = null,
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(userZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        // Act
        await service.SetTaskDueDateAsync(ListId, TaskId, new DateOnly(2026, 4, 10), "user-1");

        // Assert: no recurrence fields set on the patch
        Assert.NotNull(patchedTask);
        Assert.Null(patchedTask.Recurrence);
    }

    /// <summary>
    /// When the existing recurring task's RecurrencePattern has null AdditionalData
    /// (as can happen with certain Graph SDK / Kiota versions), rescheduling must not throw.
    /// Regression test for: "Failed to reschedule task: AdditionalData can not be null".
    /// </summary>
    [Fact]
    public async Task SetTaskDueDateAsync_RecurringTaskWithNullAdditionalData_DoesNotThrow()
    {
        // Arrange
        var userZone = TimeZoneInfo.Utc;
        var newDueDate = new DateOnly(2026, 4, 10);

        var pattern = new GraphRecurrencePattern
        {
            Type = Microsoft.Graph.Models.RecurrencePatternType.Daily,
            Interval = 1,
        };
        // Simulate Kiota deserialization leaving AdditionalData null
        pattern.AdditionalData = null!;

        var existingTask = new GraphTodoTask
        {
            IsReminderOn = false,
            ReminderDateTime = null,
            Recurrence = new GraphPatternedRecurrence
            {
                Pattern = pattern,
                Range = new GraphRecurrenceRange
                {
                    Type = Microsoft.Graph.Models.RecurrenceRangeType.NoEnd,
                    StartDate = new Date(2026, 3, 1),
                },
            },
        };

        var graphClient = Substitute.For<IGraphTodoClient>();
        graphClient.GetTaskAsync(ListId, TaskId).Returns(existingTask);

        GraphTodoTask? patchedTask = null;
        graphClient
            .When(c => c.PatchTaskAsync(ListId, TaskId, Arg.Any<GraphTodoTask>()))
            .Do(ci => patchedTask = ci.Arg<GraphTodoTask>());

        var userTimeZoneService = Substitute.For<IUserTimeZoneService>();
        userTimeZoneService.GetCurrentUserTimeZoneAsync().Returns(userZone);

        var service = new GraphTodoService(graphClient, userTimeZoneService, NullLogger<GraphTodoService>.Instance);

        // Act — must not throw
        await service.SetTaskDueDateAsync(ListId, TaskId, newDueDate, "user-1");

        // Assert: recurrence is included with updated startDate and AdditionalData is initialized
        Assert.NotNull(patchedTask);
        Assert.NotNull(patchedTask.Recurrence?.Pattern);
        Assert.NotNull(patchedTask.Recurrence!.Pattern!.AdditionalData);
        Assert.Equal(new Date(2026, 4, 10), patchedTask.Recurrence.Range!.StartDate);
    }
}
