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
        public DbSet<HistoricalConsultation> HistoricalConsultations { get; set; } = null!;
        public DbSet<WhatsAppMessage> WhatsAppMessages { get; set; } = null!;
        public DbSet<DelayEvent> DelayEvents { get; set; } = null!;
        public DbSet<QueueImpact> QueueImpacts { get; set; } = null!;

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

            // HistoricalConsultation config
            modelBuilder.Entity<HistoricalConsultation>()
                .HasOne(h => h.Doctor)
                .WithMany()
                .HasForeignKey(h => h.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HistoricalConsultation>()
                .HasIndex(h => h.DoctorId);

            modelBuilder.Entity<HistoricalConsultation>()
                .HasIndex(h => h.PatientCategory);

            modelBuilder.Entity<HistoricalConsultation>()
                .HasIndex(h => new { h.DayOfWeek, h.HourOfDay });

            // DelayEvent config
            modelBuilder.Entity<DelayEvent>()
                .HasOne(d => d.Doctor)
                .WithMany()
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DelayEvent>()
                .HasOne(d => d.Patient)
                .WithMany()
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.NoAction); // Avoid circular cascade paths

            modelBuilder.Entity<DelayEvent>()
                .HasIndex(d => d.DoctorId);

            modelBuilder.Entity<DelayEvent>()
                .HasIndex(d => d.PatientId);

            modelBuilder.Entity<DelayEvent>()
                .HasIndex(d => d.Timestamp);

            // QueueImpact config
            modelBuilder.Entity<QueueImpact>()
                .HasOne(q => q.Patient)
                .WithMany()
                .HasForeignKey(q => q.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QueueImpact>()
                .HasIndex(q => q.PatientId);

            modelBuilder.Entity<QueueImpact>()
                .HasIndex(q => q.Timestamp);
        }
    }
}
