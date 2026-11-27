using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AppointmentApi.Data;

namespace AppointmentApi.Tests;

public static class TestDbContextFactory
{
    public static (AppDbContext context, SqliteConnection connection) CreateSqliteInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}
