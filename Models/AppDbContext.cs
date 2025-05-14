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
        }
    }
}
