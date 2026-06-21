using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Services
{
    public interface IWhatsAppService
    {
        Task SendWhatsAppMessageAsync(string phoneNumber, string messageText);
        Task SendQueueAlertAsync(Patient patient, string alertType, double estWaitMinutes = 0);
        Task<IEnumerable<WhatsAppMessage>> GetMessagesForPhoneAsync(string phoneNumber);
        Task<IEnumerable<WhatsAppMessage>> GetAllMessagesAsync();
    }
}
