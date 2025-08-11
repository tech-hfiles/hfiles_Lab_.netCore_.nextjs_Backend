using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;


namespace HFiles_Backend.API.Controllers.Labs
{
    [ApiController]
    [Route("api/")]
    public class LabAdminController(
        AppDbContext context,
        IPasswordHasher<LabSuperAdmin> passwordHasher,
        JwtTokenService jwtTokenService,
        ILogger<LabAdminController> logger) : ControllerBase
    {
        private readonly AppDbContext _context = context;
        private readonly IPasswordHasher<LabSuperAdmin> _passwordHasher = passwordHasher;
        private readonly JwtTokenService _jwtTokenService = jwtTokenService;
        private readonly ILogger<LabAdminController> _logger = logger;

        public static class UserRoles
        {
            public const string SuperAdmin = "Super Admin";
            public const string Admin = "Admin";
            public const string Member = "Member";
        }




        // Create Lab Super Admin
        [HttpPost("labs/super-admins")]
        public async Task<IActionResult> CreateLabAdmin([FromBody] CreateSuperAdmin dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Received request to create Super Admin. Payload: {@dto}", dto);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                if (dto.UserId == 0 || string.IsNullOrWhiteSpace(dto.Email))
                {
                    _logger.LogWarning("UserId or Email missing in request.");
                    return BadRequest(ApiResponseFactory.Fail("UserId and Email are required in the payload."));
                }

                await using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var lab = await _context.LabSignups
                    .FirstOrDefaultAsync(l => l.Id == dto.UserId && l.Email == dto.Email)
                    .ConfigureAwait(false);

                if (lab == null)
                {
                    _logger.LogWarning("Invalid credentials provided: UserId={UserId}, Email={Email}", dto.UserId, dto.Email);
                    return NotFound(ApiResponseFactory.Fail("Invalid Credentials."));
                }

                if (lab.IsSuperAdmin)
                {
                    _logger.LogWarning("Super Admin already exists for lab {LabName}.", lab.LabName);
                    return BadRequest(ApiResponseFactory.Fail($"A Super Admin already exists for the lab {lab.LabName}."));
                }

                var userDetails = await _context.Users
                    .FirstOrDefaultAsync(u => u.HfId == dto.HFID)
                    .ConfigureAwait(false);

                if (userDetails == null)
                {
                    _logger.LogWarning("No user found with HFID {HFID}.", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail($"No user found with HFID {dto.HFID}."));
                }

                _logger.LogInformation("Creating new Super Admin for user: {UserId}, Lab: {LabId}", userDetails.Id, dto.UserId);

                var newAdmin = new LabSuperAdmin
                {
                    UserId = userDetails.Id,
                    LabId = dto.UserId,
                    PasswordHash = _passwordHasher.HashPassword(null!, dto.Password),
                    EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    IsMain = 1
                };

                _context.LabSuperAdmins.Add(newAdmin);
                lab.IsSuperAdmin = true;
                _context.LabSignups.Update(lab);

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);

                var tokenData = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, newAdmin.Id, dto.Role, 0);

                _logger.LogInformation("Super Admin created successfully. Token issued. Session ID: {SessionId}", tokenData.SessionId);

                var responseData = new
                {
                    username = $"{userDetails.FirstName} {userDetails.LastName}",
                    token = tokenData.Token,
                    sessionId = tokenData.SessionId
                };

                return Ok(ApiResponseFactory.Success(responseData, "Super Admin created successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Super Admin creation failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Users (Super Admin/Admin/Member) Login
        [HttpPost("labs/users/login")]
        public async Task<IActionResult> LabAdminLogin([FromBody] UserLogin dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received login request for HFID: {HFID}, Role: {Role}", dto.HFID, dto.Role);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var userDetails = await _context.Set<Domain.Entities.Users.User>()
                    .FirstOrDefaultAsync(u => u.HfId == dto.HFID)
                    .ConfigureAwait(false);

                if (userDetails == null)
                {
                    _logger.LogWarning("Login failed: No user found with HFID {HFID}", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail($"No Super Admin/Admin/Member found with HFID {dto.HFID}."));
                }

                var username = $"{userDetails.FirstName} {userDetails.LastName}";

                var labSignup = await _context.LabSignups
                    .FirstOrDefaultAsync(l => l.Id == dto.UserId && l.Email == dto.Email && l.DeletedBy == 0)
                    .ConfigureAwait(false);

                if (labSignup == null)
                {
                    _logger.LogWarning("Login failed: Invalid credentials for UserId={UserId}, Email={Email}", dto.UserId, dto.Email);
                    return NotFound(ApiResponseFactory.Fail($"Invalid Credentials."));
                }

                object response;

                if (dto.Role == "Super Admin")
                {
                    var admin = await _context.LabSuperAdmins
                        .FirstOrDefaultAsync(a =>
                            a.UserId == userDetails.Id &&
                            (a.LabId == dto.UserId || a.LabId == labSignup.LabReference) &&
                            a.IsMain == 1)
                        .ConfigureAwait(false);

                    if (admin == null)
                    {
                        _logger.LogWarning("Login failed: Not a Super Admin for HFID {HFID}", dto.HFID);
                        return Unauthorized(ApiResponseFactory.Fail($"The user with HFID: {dto.HFID} is not a Super Admin."));
                    }

                    if (string.IsNullOrWhiteSpace(admin.PasswordHash))
                    {
                        _logger.LogWarning("Login failed: Password not set for Super Admin {Username}", username);
                        return Unauthorized(ApiResponseFactory.Fail($"Password is not set for Super Admin: {username}"));
                    }

                    if (_passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, dto.Password) != PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("Login failed: Invalid password for Super Admin {Username}", username);
                        return Unauthorized(ApiResponseFactory.Fail("Invalid password."));
                    }

                    var (Token, SessionId) = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, admin.Id, dto.Role, 0);
                    _logger.LogInformation("Super Admin login success: {Username} | Session ID: {SessionId}", username, SessionId);

                    response = new { Username = username, Token, SessionId };
                }
                else if (dto.Role == "Admin" || dto.Role == "Member")
                {
                    var member = await _context.LabMembers
                        .FirstOrDefaultAsync(m =>
                            m.UserId == userDetails.Id &&
                            m.LabId == dto.UserId &&
                            m.DeletedBy == 0 &&
                            m.Role == dto.Role)
                        .ConfigureAwait(false);

                    if (member == null)
                    {
                        _logger.LogWarning("Login failed: {Role} not found for HFID {HFID}", dto.Role, dto.HFID);
                        return Unauthorized(ApiResponseFactory.Fail($"{dto.Role} not found. Please register first."));
                    }

                    if (string.IsNullOrWhiteSpace(member.PasswordHash))
                    {
                        _logger.LogWarning("Login failed: Password not set for {Role} {Username}", dto.Role, username);
                        return Unauthorized(ApiResponseFactory.Fail($"Password is not set for {dto.Role}: {username}"));
                    }

                    if (_passwordHasher.VerifyHashedPassword(null!, member.PasswordHash, dto.Password) != PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("Login failed: Invalid password for {Role} {Username}", dto.Role, username);
                        return Unauthorized(ApiResponseFactory.Fail("Invalid password."));
                    }

                    var tokenData = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, member.Id, dto.Role, 0);
                    _logger.LogInformation("{Role} login success: {Username} | Session ID: {SessionId}", dto.Role, username, tokenData.SessionId);

                    response = new { Username = username, tokenData.Token, tokenData.SessionId };
                }
                else
                {
                    _logger.LogWarning("Login failed: Invalid role specified {Role}", dto.Role);
                    return BadRequest(ApiResponseFactory.Fail("Invalid role specified."));
                }

                await transaction.CommitAsync().ConfigureAwait(false);
                return Ok(ApiResponseFactory.Success(response, $"{dto.Role} successfully logged in."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login Error: Unexpected failure.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Get all users (Super Admin/Admin/Members)
        [HttpGet("labs/{labId}/users")]
        public async Task<IActionResult> GetAllLabUsers([FromRoute][Range(1, int.MaxValue, ErrorMessage = "Lab ID must be greater than zero.")] int labId)
        {
            HttpContext.Items["Log-Category"] = "User Retrieval";
            _logger.LogInformation("Received request to fetch all users for Lab ID: {LabId}", labId);

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var labEntry = await _context.LabSignups
                    .FirstOrDefaultAsync(l => l.Id == labId)
                    .ConfigureAwait(false);

                if (labEntry == null)
                {
                    _logger.LogWarning("Lab with ID {LabId} not found.", labId);
                    return NotFound(ApiResponseFactory.Fail($"Lab with ID {labId} not found."));
                }

                int mainLabId = labEntry.LabReference == 0 ? labId : labEntry.LabReference;

                var superAdmin = await (
                    from a in _context.LabSuperAdmins
                    join u in _context.Users on a.UserId equals u.Id
                    where a.LabId == mainLabId && a.IsMain == 1
                    select new Application.DTOs.Labs.User
                    {
                        MemberId = a.Id,
                        HFID = u.HfId ?? string.Empty,
                        Name = $"{u.FirstName} {u.LastName}",
                        Email = u.Email ?? string.Empty,
                        Role = UserRoles.SuperAdmin,
                        ProfilePhoto = string.IsNullOrEmpty(u.ProfilePhoto) ? "No image preview available" : u.ProfilePhoto
                    }).FirstOrDefaultAsync().ConfigureAwait(false);

                var membersList = await (
                    from m in _context.LabMembers
                    join u in _context.Users on m.UserId equals u.Id
                    where m.LabId == labId && m.DeletedBy == 0
                    select new
                    {
                        MemberId = m.Id,
                        m.UserId,
                        HFID = u.HfId,
                        Name = $"{u.FirstName} {u.LastName}",
                        Email = u.Email,
                        m.Role,
                        m.CreatedBy,
                        m.PromotedBy,
                        ProfilePhoto = string.IsNullOrEmpty(u.ProfilePhoto) ? "No image preview available" : u.ProfilePhoto
                    }).ToListAsync().ConfigureAwait(false);

                _logger.LogInformation("Total Members found: {MemberCount}", membersList.Count);

                var labAdmins = await _context.LabSuperAdmins
                    .Where(a => a.LabId == labId)
                    .ToDictionaryAsync(a => a.Id).ConfigureAwait(false);

                var labMembers = await _context.LabMembers
                    .ToDictionaryAsync(m => m.Id).ConfigureAwait(false);

                var userDetails = await _context.Users
                    .ToDictionaryAsync(u => u.Id).ConfigureAwait(false);

                var memberDtos = membersList.Select(m =>
                {
                    string promotedByName = "Not Promoted Yet";
                    string createdByName = "Unknown";

                    if (labAdmins.ContainsKey(m.PromotedBy))
                        promotedByName = "Main";
                    else if (labMembers.TryGetValue(m.PromotedBy, out var promoter) &&
                             userDetails.TryGetValue(promoter.UserId, out var promoterDetails))
                        promotedByName = promoterDetails.FirstName ?? "Unknown";

                    if (labAdmins.ContainsKey(m.CreatedBy))
                        createdByName = "Main";
                    else if (labMembers.TryGetValue(m.CreatedBy, out var creator) &&
                             userDetails.TryGetValue(creator.UserId, out var creatorDetails))
                        createdByName = creatorDetails.LastName ?? "Unknown";

                    return new Application.DTOs.Labs.User
                    {
                        MemberId = m.MemberId,
                        HFID = m.HFID,
                        Name = m.Name,
                        Email = m.Email,
                        Role = m.Role,
                        CreatedByName = createdByName,
                        PromotedByName = promotedByName,
                        ProfilePhoto = m.ProfilePhoto
                    };
                }).ToList();

                if (superAdmin == null && memberDtos.Count == 0)
                {
                    _logger.LogWarning("No active admins or members found for Lab ID {LabId}.", labId);
                    return NotFound(ApiResponseFactory.Fail($"No active admins or members found for Lab ID {labId}."));
                }

                await tx.CommitAsync().ConfigureAwait(false);

                var response = new
                {
                    LabId = labId,
                    MainLabId = mainLabId,
                    UserCounts = memberDtos.Count + 1,
                    SuperAdmin = superAdmin,
                    Members = memberDtos
                };

                _logger.LogInformation("Successfully fetched users for Lab ID {LabId}.", labId);
                return Ok(ApiResponseFactory.Success(response, "Users fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User retrieval failed for Lab ID {LabId}.", labId);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Promotes Admin to Super Admin
        [HttpPost("labs/admin/promote")]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<IActionResult> PromoteLabMemberToSuperAdmin([FromBody] PromoteAdmin dto)
        {
            HttpContext.Items["Log-Category"] = "Role Management";
            _logger.LogInformation("Received promotion request for Member ID: {MemberId}", dto.MemberId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                var labAdminIdClaim = User.Claims.FirstOrDefault(c => c.Type == "LabAdminId");
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");

                if (labAdminIdClaim == null || !int.TryParse(labAdminIdClaim.Value, out int labAdminId))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing Super Admin Id in token."));

                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int labId))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing labId in token."));

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var loggedInLab = await _context.LabSignups
                    .FirstOrDefaultAsync(l => l.Id == labId).ConfigureAwait(false);

                if (loggedInLab == null)
                    return BadRequest(ApiResponseFactory.Fail("Lab not found"));

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                var branchIds = await _context.LabSignups
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync().ConfigureAwait(false);

                branchIds.Add(mainLabId);

                var member = await _context.LabMembers
                    .FirstOrDefaultAsync(m =>
                        m.Id == dto.MemberId &&
                        m.DeletedBy == 0 &&
                        (branchIds.Contains(m.LabId) || m.LabId == mainLabId))
                    .ConfigureAwait(false);

                if (member == null)
                    return NotFound(ApiResponseFactory.Fail($"No lab member found or not eligible for promotion."));

                member.DeletedBy = labAdminId;
                _context.LabMembers.Update(member);

                var currentSuperAdmin = await _context.LabSuperAdmins
                    .FirstOrDefaultAsync(a => a.IsMain == 1 && a.LabId == mainLabId)
                    .ConfigureAwait(false);

                if (currentSuperAdmin == null)
                    return NotFound(ApiResponseFactory.Fail($"No active Super Admin found."));

                currentSuperAdmin.IsMain = 0;
                _context.LabSuperAdmins.Update(currentSuperAdmin);

                long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var existedSuperAdmin = await _context.LabSuperAdmins
                    .FirstOrDefaultAsync(a => a.UserId == member.UserId && a.LabId == mainLabId && a.IsMain == 0)
                    .ConfigureAwait(false);

                var existedMember = await _context.LabMembers
                    .FirstOrDefaultAsync(m =>
                        m.UserId == currentSuperAdmin.UserId &&
                        (branchIds.Contains(m.LabId) || m.LabId == currentSuperAdmin.LabId) &&
                        m.DeletedBy != 0)
                    .ConfigureAwait(false);

                LabSuperAdmin newSuperAdmin;
                LabMember newLabMember;

                if (existedSuperAdmin != null)
                {
                    existedSuperAdmin.IsMain = 1;
                    existedSuperAdmin.PasswordHash = member.PasswordHash;
                    existedSuperAdmin.EpochTime = epoch;
                    _context.LabSuperAdmins.Update(existedSuperAdmin);
                    newSuperAdmin = existedSuperAdmin;
                }
                else
                {
                    newSuperAdmin = new LabSuperAdmin
                    {
                        UserId = member.UserId,
                        LabId = mainLabId,
                        PasswordHash = member.PasswordHash,
                        EpochTime = epoch,
                        IsMain = 1
                    };
                    _context.LabSuperAdmins.Add(newSuperAdmin);
                }

                if (existedMember != null)
                {
                    existedMember.LabId = loggedInLab.Id;
                    existedMember.Role = "Admin";
                    existedMember.DeletedBy = 0;
                    existedMember.PromotedBy = newSuperAdmin.Id;
                    existedMember.PasswordHash = currentSuperAdmin.PasswordHash;
                    existedMember.EpochTime = epoch;
                    _context.LabMembers.Update(existedMember);
                    newLabMember = existedMember;
                }
                else
                {
                    newLabMember = new LabMember
                    {
                        UserId = currentSuperAdmin.UserId,
                        LabId = loggedInLab.Id,
                        Role = "Admin",
                        PasswordHash = currentSuperAdmin.PasswordHash,
                        CreatedBy = currentSuperAdmin.Id,
                        DeletedBy = 0,
                        PromotedBy = currentSuperAdmin.Id,
                        EpochTime = epoch
                    };
                    _context.LabMembers.Add(newLabMember);
                }

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                var oldSuperAdminUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == currentSuperAdmin.UserId)
                    .ConfigureAwait(false);

                var newSuperAdminUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == member.UserId)
                    .ConfigureAwait(false);

                string newSuperAdminName = $"{newSuperAdminUser?.FirstName} {newSuperAdminUser?.LastName}".Trim();
                string oldSuperAdminName = $"{oldSuperAdminUser?.FirstName} {oldSuperAdminUser?.LastName}".Trim();

                var response = new
                {
                    NewSuperAdminId = newSuperAdmin.Id,
                    OldSuperAdminId = currentSuperAdmin.Id,
                    NewMemberId = newLabMember.Id,
                    OldMemberId = member.Id,
                    UpdatedDeletedBy = member.DeletedBy,
                    NewSuperAdminUsername = newSuperAdminName,
                    OldSuperAdminUsername = oldSuperAdminName,
                    BranchLabId = member.LabId != mainLabId ? member.LabId : 0,
                    NotificationContext = new
                    {
                        PromotedTo = "Super Admin",
                        NewSuperAdminName = newSuperAdminName,
                        OldSuperAdminName = oldSuperAdminName
                    },
                    NotificationMessage = $"{newSuperAdminName} has been promoted to Super Admin, replacing {oldSuperAdminName}."
                };


                _logger.LogInformation("Promotion successful. New SuperAdmin: {NewSuperAdminId}, Old SuperAdmin: {OldSuperAdminId}", newSuperAdmin.Id, currentSuperAdmin.Id);

                return Ok(ApiResponseFactory.Success(response, $"{member.Role} promoted to Super Admin successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Promotion failed due to unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }
    }
}