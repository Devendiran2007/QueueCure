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
            // Ensure database is recreated for development schema changes
            context.Database.EnsureDeleted();
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
                if (!context.Patients.Any())
                {
                    SeedPatients(context);
                }
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

            SeedPatients(context);
        }

        private static void SeedPatients(QueueCureDbContext context)
        {
            var doc1 = context.Doctors.FirstOrDefault(d => d.Name == "Dr. Albert Stone");
            var doc2 = context.Doctors.FirstOrDefault(d => d.Name == "Dr. Betty Smith");

            if (doc1 != null && doc2 != null)
            {
                var now = DateTime.UtcNow;
                int seq = 1;

                void SeedCompletedPatient(string name, VisitCategory category, Guid doctorId, int durationMinutes)
                {
                    var prefix = doctorId == doc1.Id ? "101" : "102";
                    var patient = new Patient
                    {
                        Name = name,
                        PhoneNumber = "+1 555-0100",
                        TokenNumber = $"{prefix}-{seq++:D3}",
                        CheckInTime = now.AddMinutes(-60),
                        Status = PatientStatus.Completed,
                        Category = category,
                        DoctorId = doctorId
                    };
                    context.Patients.Add(patient);
                    context.SaveChanges();

                    var checkInEvent = new QueueEvent
                    {
                        PatientId = patient.Id,
                        EventType = "CheckIn",
                        Timestamp = now.AddMinutes(-60)
                    };

                    var startedEvent = new QueueEvent
                    {
                        PatientId = patient.Id,
                        EventType = "Started",
                        Timestamp = now.AddMinutes(-45)
                    };

                    var completedEvent = new QueueEvent
                    {
                        PatientId = patient.Id,
                        EventType = "Completed",
                        Timestamp = now.AddMinutes(-45 + durationMinutes)
                    };

                    context.QueueEvents.AddRange(checkInEvent, startedEvent, completedEvent);
                }

                // Fever: average 8 minutes (seeded: 10 mins and 6 mins)
                SeedCompletedPatient("Fever Patient 1", VisitCategory.Fever, doc2.Id, 10);
                SeedCompletedPatient("Fever Patient 2", VisitCategory.Fever, doc2.Id, 6);

                // Cardiology: average 25 minutes (seeded: 30 mins and 20 mins)
                SeedCompletedPatient("Cardiology Patient 1", VisitCategory.CardiologyConsultation, doc1.Id, 30);
                SeedCompletedPatient("Cardiology Patient 2", VisitCategory.CardiologyConsultation, doc1.Id, 20);

                // Diabetes Review: average 15 minutes (seeded: 18 mins and 12 mins)
                SeedCompletedPatient("Diabetes Patient 1", VisitCategory.DiabetesReview, doc2.Id, 18);
                SeedCompletedPatient("Diabetes Patient 2", VisitCategory.DiabetesReview, doc2.Id, 12);

                context.SaveChanges();
            }
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
