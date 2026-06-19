using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using QueueCure.Models;
using QueueCure.Repositories;

namespace QueueCure.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IQueueRepository _queueRepository;

        public UserService(IUserRepository userRepository, IQueueRepository queueRepository)
        {
            _userRepository = userRepository;
            _queueRepository = queueRepository;
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _userRepository.GetByIdAsync(id);
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null) return null;

            var hashedInput = HashPassword(password);
            if (user.PasswordHash != hashedInput) return null;

            return user;
        }

        public async Task<User> RegisterAsync(string username, string password, string fullName, UserRole role, string? specialty = null, string? roomNumber = null)
        {
            var existing = await _userRepository.GetByUsernameAsync(username);
            if (existing != null)
            {
                throw new InvalidOperationException("Username already exists.");
            }

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Role = role
            };

            await _userRepository.AddAsync(user);

            // If user is a doctor, create corresponding Doctor record
            if (role == UserRole.Doctor)
            {
                var doctor = new Doctor
                {
                    UserId = user.Id,
                    Specialty = specialty ?? "General Physician",
                    RoomNumber = roomNumber ?? "N/A",
                    IsAvailable = true
                };
                await _queueRepository.AddDoctorAsync(doctor);
            }

            return user;
        }

        private string HashPassword(string password)
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
