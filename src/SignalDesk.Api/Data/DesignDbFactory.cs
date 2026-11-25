using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SignalDesk.Api.Data;

public sealed class DesignDbFactory : IDesignTimeDbContextFactory<DeskDb>
{
    public DeskDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<DeskDb>()
        .UseSqlite("Data Source=design-only.db").Options);
}
