using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories	
{
	public class ClinicEnquiryRepository : IClinicEnquiryRepository
	{
		private readonly AppDbContext _context;
		private readonly ILogger<ClinicEnquiryRepository> _logger;

		public ClinicEnquiryRepository(AppDbContext context, ILogger<ClinicEnquiryRepository> logger)
		{
			_context = context;
			_logger = logger;
		}

		public async Task AddAsync(ClinicEnquiry enquiry)
		{
			try
			{
				await _context.clinicEnquiry.AddAsync(enquiry);
				await _context.SaveChangesAsync();
				_logger.LogInformation("Created new enquiry ID {EnquiryId}", enquiry.Id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create enquiry for Clinic ID {ClinicId}", enquiry.ClinicId);
				throw;
			}
		}

		public async Task<List<ClinicEnquiry>> GetAllAsync(int clinicId)
		{
			return await _context.clinicEnquiry
				.AsNoTracking()
				.Where(e => e.ClinicId == clinicId)
				.OrderByDescending(e => e.EpochTime)
				.ToListAsync();
		}

		public async Task<ClinicEnquiry?> GetByIdAsync(int id)
		{
			return await _context.clinicEnquiry
				.FirstOrDefaultAsync(e => e.Id == id);
		}

		public async Task UpdateAsync(ClinicEnquiry enquiry)
		{
			try
			{
				_context.clinicEnquiry.Update(enquiry);
				await _context.SaveChangesAsync();
				_logger.LogInformation("Updated enquiry ID {EnquiryId}", enquiry.Id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to update enquiry ID {EnquiryId}", enquiry.Id);
				throw;
			}
		}
	}
}
