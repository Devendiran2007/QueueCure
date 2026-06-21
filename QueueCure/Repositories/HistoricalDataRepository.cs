using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QueueCure.Data;
using QueueCure.Models;

namespace QueueCure.Repositories
{
    public class HistoricalDataRepository : IHistoricalDataRepository
    {
        private readonly QueueCureDbContext _context;

        public HistoricalDataRepository(QueueCureDbContext context)
        {
            _context = context;
        }

        public async Task AddHistoricalRecordAsync(HistoricalConsultation record)
        {
            await _context.HistoricalConsultations.AddAsync(record);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<HistoricalConsultation>> GetHistoryByDoctorAsync(Guid doctorId)
        {
            return await _context.HistoricalConsultations
                .Where(h => h.DoctorId == doctorId)
                .OrderByDescending(h => h.ConsultationEndTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<HistoricalConsultation>> GetHistoryByCategoryAsync(VisitCategory category)
        {
            return await _context.HistoricalConsultations
                .Where(h => h.PatientCategory == category)
                .OrderByDescending(h => h.ConsultationEndTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<HistoricalConsultation>> GetAllHistoryAsync()
        {
            return await _context.HistoricalConsultations
                .OrderByDescending(h => h.ConsultationEndTime)
                .ToListAsync();
        }

        public async Task<int> GetRecordCountAsync()
        {
            return await _context.HistoricalConsultations.CountAsync();
        }
    }
}
