using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TorneioTenisMesa.Models
{
    // Definindo User com chave primária do tipo int
    public class AppDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<ImportBatch> ImportBatches { get; set; }  // DbSet para o ImportBatch

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // sempre chamar antes ou depois das configurações

            // Game → Player1
            modelBuilder.Entity<Game>()
                .HasOne(g => g.Player1)
                .WithMany()
                .HasForeignKey(g => g.Player1Id)
                .OnDelete(DeleteBehavior.NoAction);

            // Game → Player2
            modelBuilder.Entity<Game>()
                .HasOne(g => g.Player2)
                .WithMany()
                .HasForeignKey(g => g.Player2Id)
                .OnDelete(DeleteBehavior.NoAction);

            // Game → Tournament
            modelBuilder.Entity<Game>()
                .HasOne(g => g.Tournament)
                .WithMany(t => t.Games)
                .HasForeignKey(g => g.TournamentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Player → Tournament
            modelBuilder.Entity<Player>()
                .HasOne(p => p.Tournament)
                .WithMany(t => t.Players)
                .HasForeignKey(p => p.TournamentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Player → User
            modelBuilder.Entity<Player>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.ClientNoAction);

            // Tournament → Admin User
            modelBuilder.Entity<Tournament>()
                .HasOne<User>(t => t.AdminUser)
                .WithMany()
                .HasForeignKey(t => t.AdminUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Player → ImportBatch
            modelBuilder.Entity<Player>()
                .HasOne(p => p.ImportBatch)  // O Player tem um ImportBatch
                .WithMany()  // ImportBatch pode ter muitos jogadores
                .HasForeignKey(p => p.ImportBatchId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relacionamento entre ImportBatch e Players (configuração simplificada)
            modelBuilder.Entity<ImportBatch>()
                .HasMany(p => p.Players)  // ImportBatch tem muitos Players
                .WithOne(p => p.ImportBatch)  // Cada Player tem um ImportBatch
                .HasForeignKey(p => p.ImportBatchId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
