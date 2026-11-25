using Microsoft.EntityFrameworkCore;

namespace SignalDesk.Api.Data;

public sealed class DeskDb(DbContextOptions<DeskDb> options) : DbContext(options)
{
    public DbSet<DatasetRow> Dataset => Set<DatasetRow>();
    public DbSet<CompetitorRow> Competitors => Set<CompetitorRow>();
    public DbSet<PublisherRow> Publishers => Set<PublisherRow>();
    public DbSet<SourceRow> Sources => Set<SourceRow>();
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();
    public DbSet<SignalRow> Signals => Set<SignalRow>();
    public DbSet<ProfileFact> Facts => Set<ProfileFact>();
    public DbSet<ActivityRow> Activities => Set<ActivityRow>();
    public DbSet<AiExecution> AiExecutions => Set<AiExecution>();
    public DbSet<ReviewRow> Reviews => Set<ReviewRow>();
    public DbSet<SearchHitRow> SearchHits => Set<SearchHitRow>();
    public DbSet<CollectionRun> Runs => Set<CollectionRun>();
    public DbSet<JobRecord> Jobs => Set<JobRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<JobRecord>().HasKey(x => x.TrackingID);
        model.Entity<JobRecord>().HasIndex(x => new { x.QueueID, x.IsComplete, x.ExecuteAfter });
        model.Entity<SourceRow>().HasIndex(x => x.Url).IsUnique();
        model.Entity<DocumentRow>().HasIndex(x => new { x.CanonicalUrl, x.ContentHash }).IsUnique();
        model.Entity<SignalRow>().HasIndex(x => new { x.PublishedUtc, x.Id });
        model.Entity<SignalRow>().ToTable(t => t.HasCheckConstraint("CK_Importance", "\"Importance\" BETWEEN 0 AND 100"));
        model.Entity<AiExecution>().HasIndex(x => new { x.Provider, x.Operation, x.InputHash, x.CreatedUtc });
        model.Entity<ProfileFact>().HasIndex(x => new { x.CompetitorId, x.DocumentId, x.Kind, x.Label }).IsUnique();
        model.Entity<ReviewRow>().HasIndex(x => x.DocumentId).IsUnique();
        foreach (var relation in model.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            relation.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
