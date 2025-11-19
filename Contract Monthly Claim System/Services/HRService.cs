#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Contract_Monthly_Claim_System.Services
{
    public class HRService
    {
        private readonly ApplicationDbContext _db;

        public HRService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<HRDashboardVM> GetDashboardAsync()
        {
            var approvedQuery = _db.Claims
                .Where(c => c.Status == "Approved");

            var approvedClaims = await approvedQuery
                .Include(c => c.Documents)
                .Include(c => c.LecturerUser)
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var totalPayableValue = await approvedQuery
                .Select(c => (decimal?)c.Total)
                .SumAsync();

            var lecturers = await _db.Users
                .Where(u => u.Role == "Lecturer")
                .OrderBy(u => u.FullName)
                .AsNoTracking()
                .ToListAsync();

            var totalApproved = approvedClaims.Count;
            var totalPayable = Math.Round(totalPayableValue ?? 0m, 2, MidpointRounding.AwayFromZero);

            return new HRDashboardVM
            {
                TotalApprovedClaims = totalApproved,
                PendingPayments = totalApproved,
                TotalPayableAmount = totalPayable,
                ApprovedClaims = approvedClaims,
                Lecturers = lecturers
            };
        }

        public async Task<LecturerProfileVM?> GetLecturerProfileAsync(string lecturerUserId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == lecturerUserId);
            if (user == null) return null;

            var claims = await _db.Claims
                .Where(c => c.LecturerUserId == lecturerUserId)
                .Include(c => c.Documents)
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return new LecturerProfileVM
            {
                Lecturer = user,
                Claims = claims,
                NewEmail = user.Email,
                NewPhoneNumber = user.PhoneNumber
            };
        }

        public async Task<List<ApprovedClaimSummaryDTO>> GetApprovedSummariesAsync()
        {
            var rawClaims = await _db.Claims
                .Where(c => c.Status == "Approved")
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    c.LecturerName,
                    c.CreatedAt,
                    c.Total,
                    c.Status
                })
                .ToListAsync();

            return rawClaims.Select(c => new ApprovedClaimSummaryDTO
            {
                ClaimId = c.Id,
                LecturerName = c.LecturerName,
                ClaimDate = c.CreatedAt,
                TotalAmount = Math.Round((decimal)c.Total, 2, MidpointRounding.AwayFromZero),
                Status = c.Status
            }).ToList();
        }
    }
}
