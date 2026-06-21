using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QueueCure.Data;
using QueueCure.Models;

namespace QueueCure.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly QueueCureDbContext _context;

        public WhatsAppService(QueueCureDbContext context)
        {
            _context = context;
        }

        public async Task SendWhatsAppMessageAsync(string phoneNumber, string messageText)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return;

            var message = new WhatsAppMessage
            {
                PhoneNumber = phoneNumber,
                MessageText = messageText,
                SentAt = DateTime.UtcNow,
                Status = "Delivered"
            };

            await _context.WhatsAppMessages.AddAsync(message);
            await _context.SaveChangesAsync();
        }

        public async Task SendQueueAlertAsync(Patient patient, string alertType, double estWaitMinutes = 0)
        {
            if (patient == null || string.IsNullOrWhiteSpace(patient.PhoneNumber)) return;

            string doctorName = patient.Doctor?.Name ?? "Your Doctor";
            string roomNumber = patient.Doctor?.RoomNumber ?? "N/A";
            string tokenNumber = patient.TokenNumber;
            string patientName = patient.Name;

            string messageText = "";

            switch (alertType.ToLower())
            {
                case "registration":
                    var estStart = DateTime.UtcNow.AddMinutes(estWaitMinutes);
                    // Add 5.5 hours for IST display or format to localized text
                    string estStartStr = estStart.AddHours(5.5).ToString("hh:mm tt");
                    messageText = $"*QueueCure AI+ Ticket*\n" +
                                  $"Hello *{patientName}*, your token has been registered successfully!\n\n" +
                                  $"• *Token Number:* {tokenNumber}\n" +
                                  $"• *Consultant:* {doctorName}\n" +
                                  $"• *Assigned Room:* Room {roomNumber}\n" +
                                  $"• *Est. Wait Time:* ~{estWaitMinutes} Mins\n" +
                                  $"• *Est. Start Time:* {estStartStr}\n\n" +
                                  $"Track your live queue position and get status alerts here:\n" +
                                  $"http://localhost:5007/patient/tracker.html?token={tokenNumber}\n\n" +
                                  $"Thank you for using QueueCure!";
                    break;

                case "called":
                    messageText = $"🔔 *YOUR TURN HAS ARRIVED!*\n\n" +
                                  $"Hello *{patientName}*, your token *{tokenNumber}* is now called.\n\n" +
                                  $"Please proceed immediately to *Room {roomNumber}* for your consultation with *{doctorName}*.\n\n" +
                                  $"View live status: http://localhost:5007/patient/tracker.html?token={tokenNumber}";
                    break;

                case "skipped":
                    messageText = $"⚠️ *TICKET SKIPPED*\n\n" +
                                  $"Hello *{patientName}*, your token *{tokenNumber}* was called but marked as absent/no-show.\n\n" +
                                  $"If you are still in the clinic, please proceed to the Front Desk to restore your place in the queue.\n\n" +
                                  $"View details: http://localhost:5007/patient/tracker.html?token={tokenNumber}";
                    break;

                case "restored":
                    messageText = $"🔄 *TICKET RESTORED*\n\n" +
                                  $"Hello *{patientName}*, your token *{tokenNumber}* has been restored to the active queue for *{doctorName}* (Room {roomNumber}).\n\n" +
                                  $"• *New Est. Wait Time:* ~{estWaitMinutes} Mins\n\n" +
                                  $"Track live: http://localhost:5007/patient/tracker.html?token={tokenNumber}";
                    break;

                default:
                    return;
            }

            await SendWhatsAppMessageAsync(patient.PhoneNumber, messageText);
        }

        public async Task<IEnumerable<WhatsAppMessage>> GetMessagesForPhoneAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return Enumerable.Empty<WhatsAppMessage>();
            
            return await _context.WhatsAppMessages
                .Where(m => m.PhoneNumber == phoneNumber)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<WhatsAppMessage>> GetAllMessagesAsync()
        {
            return await _context.WhatsAppMessages
                .OrderByDescending(m => m.SentAt)
                .Take(100)
                .ToListAsync();
        }
    }
}
