using Core.DB.Entity;

using Microsoft.EntityFrameworkCore;

namespace Core.DB {
    public class ScheduleDbContext : DbContext {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("TelegramBotConnectionString"));
#if DEBUG
            optionsBuilder.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
            optionsBuilder.EnableSensitiveDataLogging(true);
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {

        }

        public void ClearContext() {
            foreach(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in ChangeTracker.Entries()) {
                if(entry.State == EntityState.Added) {
                    entry.State = EntityState.Detached;
                } else if(entry.State is EntityState.Modified or EntityState.Deleted) {
                    entry.State = EntityState.Unchanged;
                }
            }
        }

#pragma warning disable CS8618
        public DbSet<TelegramUser> TelegramUsers { get; set; }
        public DbSet<EGS> EGS { get; set; }
    }
}
