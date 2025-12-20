using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicMemberRecordRepository: IClinicMemberRecordRepository
    {
        private readonly AppDbContext _context;

        public ClinicMemberRecordRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(ClinicMemberRecord record)
        {
            _context.clinicMemberRecords.Add(record);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ClinicMemberRecord>> GetByClinicAndUserAsync(int clinicId, int userId)
        {
            return await _context.clinicMemberRecords
                .Where(x =>
                    x.ClinicId == clinicId &&
                    x.UserId == userId &&
                    x.DeletedBy == 0)
                .OrderByDescending(x => x.EpochTime)
                .ToListAsync();
        }
    }
}

