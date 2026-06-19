using Microsoft.EntityFrameworkCore;
using QueueCure.Models;

namespace QueueCure.Data
{
    public class QueueCureDbContext : DbContext
    {
        public QueueCureDbContext(DbContextOptions<QueueCureDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Doctor> Doctors { get; set; } = null!;
        public DbSet<Patient> Patients { get; set; } = null!;
        public DbSet<QueueSettings> QueueSettings { get; set; } = null!;
        public DbSet<QueueEvent> QueueEvents { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User config
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Doctor config
            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Patient config
            modelBuilder.Entity<Patient>()
                .HasOne(p => p.Doctor)
                .WithMany()
                .HasForeignKey(p => p.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.TokenNumber);

            modelBuilder.Entity<Patient>()
                .HasIndex(p => new { p.DoctorId, p.Status });

            // QueueEvent config
            modelBuilder.Entity<QueueEvent>()
                .HasOne(q => q.Patient)
                .WithMany()
                .HasForeignKey(q => q.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QueueEvent>()
                .HasIndex(q => q.PatientId);

            modelBuilder.Entity<QueueEvent>()
                .HasIndex(q => q.Timestamp);

            // Notification config
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Patient)
                .WithMany()
                .HasForeignKey(n => n.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.PatientId);
        }
    }
}
