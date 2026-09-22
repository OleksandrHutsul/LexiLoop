using LexiLoop.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexiLoop.Data;

public sealed class LexiLoopDbContext(DbContextOptions<LexiLoopDbContext> options) : DbContext(options)
{
    public DbSet<TelegramUser> Users => Set<TelegramUser>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<VocabularyItem> VocabularyItems => Set<VocabularyItem>();
    public DbSet<LearningProgress> LearningProgress => Set<LearningProgress>();
    public DbSet<ReviewHistory> ReviewHistory => Set<ReviewHistory>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<VocabularyItemCollection> VocabularyItemCollections => Set<VocabularyItemCollection>();
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();
    public DbSet<LearningSessionCard> LearningSessionCards => Set<LearningSessionCard>();

    public override int SaveChanges()
    {
        NormalizeUtcTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeUtcTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<TelegramUser>(e =>
        {
            e.HasIndex(x => x.TelegramUserId).IsUnique();
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.FirstName).HasMaxLength(128);
            e.HasOne(x => x.Settings).WithOne(x => x.User).HasForeignKey<UserSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<VocabularyItem>(e =>
        {
            e.Property(x => x.ForeignText).HasMaxLength(500);
            e.Property(x => x.Translation).HasMaxLength(500);
            e.Property(x => x.Pronunciation).HasMaxLength(500);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.ForeignText, x.Translation }).IsUnique();
            e.HasOne(x => x.Progress).WithOne(x => x.VocabularyItem).HasForeignKey<LearningProgress>(x => x.VocabularyItemId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<LearningProgress>(e =>
        {
            e.HasIndex(x => x.ForeignToTranslationNextReviewAt);
            e.HasIndex(x => x.TranslationToForeignNextReviewAt);
            e.Property(x => x.ForeignToTranslationDifficulty).HasDefaultValue(5d);
            e.Property(x => x.TranslationToForeignDifficulty).HasDefaultValue(5d);
        });
        model.Entity<ReviewHistory>(e =>
        {
            e.HasIndex(x => x.VocabularyItemId);
            e.HasIndex(x => x.ReviewedAt);
        });
        model.Entity<Collection>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.ShareToken).HasMaxLength(64);
            e.Property(x => x.ImportedFromShareToken).HasMaxLength(64);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            e.HasIndex(x => x.ShareToken).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ImportedFromShareToken }).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.Collections).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<VocabularyItemCollection>(e =>
        {
            e.HasKey(x => new { x.VocabularyItemId, x.CollectionId });
            e.HasIndex(x => x.CollectionId);
            e.HasOne(x => x.VocabularyItem).WithMany(x => x.Collections).HasForeignKey(x => x.VocabularyItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Collection).WithMany(x => x.VocabularyItems).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<LearningSession>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasIndex(x => x.CollectionId);
            e.Property(x => x.SourceName).IsRequired().HasMaxLength(100).HasDefaultValue("All words");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Cards).WithOne(x => x.Session).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<LearningSessionCard>(e =>
        {
            e.HasIndex(x => new { x.SessionId, x.Position }).IsUnique();
            e.HasIndex(x => x.VocabularyItemId);
            e.Property(x => x.EnteredAnswer).HasMaxLength(4096);
            e.Property(x => x.ExpectedAnswer).HasMaxLength(500);
        });
    }

    private void NormalizeUtcTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified))
        foreach (var property in entry.Properties)
        {
            if (property.CurrentValue is DateTimeOffset value && value.Offset != TimeSpan.Zero)
                property.CurrentValue = value.ToUniversalTime();
        }
    }
}
