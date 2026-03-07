using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace TodoExtended.Web.Data;

/// <summary>
/// Enables SQLite foreign key enforcement on every connection.
/// SQLite disables foreign key support by default, so ON DELETE CASCADE constraints
/// (e.g., deleting CachedTask rows when a CachedTaskList is removed) would silently
/// be skipped without this interceptor.
/// </summary>
public sealed class EnableForeignKeysInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        base.ConnectionOpened(connection, eventData);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON";
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
