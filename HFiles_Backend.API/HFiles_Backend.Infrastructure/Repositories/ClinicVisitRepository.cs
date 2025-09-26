﻿using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicVisitRepository : IClinicVisitRepository
    {
        private readonly AppDbContext _context;

        public ClinicVisitRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ClinicPatient> GetOrCreatePatientAsync(string hfid, string fullName)
        {
            var patient = await _context.ClinicPatients
                .Include(p => p.Visits)
                .FirstOrDefaultAsync(p => p.HFID == hfid);

            if (patient != null) return patient;

            patient = new ClinicPatient
            {
                HFID = hfid,
                PatientName = fullName
            };

            _context.ClinicPatients.Add(patient);
            await _context.SaveChangesAsync();
            return patient;
        }

        public async Task<List<ClinicConsentForm>> GetConsentFormsByTitlesAsync(List<string> titles)
        {
            return await _context.ClinicConsentForms
                .Where(f => titles.Contains(f.Title))
                .ToListAsync();
        }

        public async Task SaveVisitAsync(ClinicVisit visit)
        {
            _context.ClinicVisits.Add(visit);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasVisitInClinicAsync(string hfid, int clinicId)
        {
            return await _context.ClinicVisits
                .AnyAsync(v => v.Patient.HFID == hfid && v.ClinicId == clinicId);
        }
        public async Task<List<ClinicVisit>> GetVisitsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicVisits
                .Include(v => v.Patient)
                .Where(v => v.ClinicId == clinicId)
                .ToListAsync();
        }
        public async Task<ClinicVisitConsentForm?> GetVisitConsentFormAsync(int visitConsentFormId)
        {
            return await _context.ClinicVisitConsentForms
                .Include(v => v.ConsentForm)
                .Include(v => v.Visit)
                .FirstOrDefaultAsync(v => v.Id == visitConsentFormId);
        }

        public async Task<ClinicVisit?> GetByIdAsync(int visitId)
       => await _context.ClinicVisits.FindAsync(visitId);

        public async Task UpdateAsync(ClinicVisit visit)
        {
            _context.ClinicVisits.Update(visit);
            await _context.SaveChangesAsync();
        }

        public async Task<ClinicVisit?> GetExistingVisitAsync(int clinicPatientId, DateTime appointmentDate)
        {
            return await _context.ClinicVisits
                .FirstOrDefaultAsync(v =>
                    v.ClinicPatientId == clinicPatientId &&
                    v.AppointmentDate.Date == appointmentDate.Date);
        }
    }
}
