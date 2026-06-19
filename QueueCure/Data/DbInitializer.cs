using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using QueueCure.Models;

namespace QueueCure.Data
{
    public static class DbInitializer
    {
        public static void Initialize(QueueCureDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Seed global settings if empty
            if (!context.QueueSettings.Any())
            {
                context.QueueSettings.Add(new QueueSettings
                {
                    AverageConsultationTime = 10,
                    LastTokenNumber = "Q-000"
                });
                context.SaveChanges();
            }

            // Look for any users
            if (context.Users.Any())
            {
                return;   // DB has been seeded
            }

            // Seed Users
            var receptionist = new User
            {
                Username = "receptionist_1",
                PasswordHash = HashPassword("receptionist"),
                FullName = "Alice Johnson",
                Role = UserRole.Receptionist
            };

            var doctorUser1 = new User
            {
                Username = "doctor_1",
                PasswordHash = HashPassword("doctor"),
                FullName = "Dr. Albert Stone",
                Role = UserRole.Doctor
            };

            var doctorUser2 = new User
            {
                Username = "doctor_2",
                PasswordHash = HashPassword("doctor"),
                FullName = "Dr. Betty Smith",
                Role = UserRole.Doctor
            };

            context.Users.AddRange(receptionist, doctorUser1, doctorUser2);
            context.SaveChanges();

            // Seed Doctors linked to user accounts
            var doctor1 = new Doctor
            {
                UserId = doctorUser1.Id,
                Name = "Dr. Albert Stone",
                Specialization = "Cardiology",
                AverageConsultationTime = 12 // custom time for Альберт
            };

            var doctor2 = new Doctor
            {
                UserId = doctorUser2.Id,
                Name = "Dr. Betty Smith",
                Specialization = "Pediatrics",
                AverageConsultationTime = 8 // custom time for Бетти
            };

            context.Doctors.AddRange(doctor1, doctor2);
            context.SaveChanges();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
