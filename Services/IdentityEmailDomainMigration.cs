

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepPortal.Data;

namespace RepPortal.Services;

public sealed class IdentityEmailDomainMigration
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IdentityEmailDomainMigration> _logger;

    public IdentityEmailDomainMigration(
        UserManager<ApplicationUser> userManager,
        ILogger<IdentityEmailDomainMigration> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        const string oldDomain = "chapinmfg.com";
        const string newDomain = "chapinusa.com";

        var users = _userManager.Users
            .Where(u => u.Email != null && u.Email.EndsWith("@" + oldDomain) )
            .AsNoTracking()
            .ToList();

        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var snapshot in users)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(snapshot.Id.ToString());
                if (user == null)
                {
                    skipped++;
                    _logger.LogWarning("Skipped user id {UserId}: no longer found.", snapshot.Id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    skipped++;
                    _logger.LogWarning("Skipped user id {UserId}: Email is blank.", user.Id);
                    continue;
                }

                if (!user.Email.EndsWith("@" + oldDomain, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    _logger.LogInformation("Skipped {Email}: no longer on old domain.", user.Email);
                    continue;
                }

                var atIndex = user.Email.IndexOf('@');
                if (atIndex < 0)
                {
                    skipped++;
                    _logger.LogWarning("Skipped user id {UserId}: invalid email format {Email}.", user.Id, user.Email);
                    continue;
                }

                var localPart = user.Email.Substring(0, atIndex);
                var newEmail = localPart + "@" + newDomain;

                // Collision check by email
                var existingByEmail = await _userManager.FindByEmailAsync(newEmail);
                if (existingByEmail != null && existingByEmail.Id.ToString() != user.Id.ToString())
                {
                    skipped++;
                    _logger.LogWarning(
                        "Skipped {OldEmail}: target email {NewEmail} already belongs to user id {ExistingUserId}.",
                        user.Email, newEmail, existingByEmail.Id);
                    continue;
                }

                // Collision check by username
                var existingByName = await _userManager.FindByNameAsync(newEmail);
                if (existingByName != null && existingByName.Id.ToString() != user.Id.ToString())
                {
                    skipped++;
                    _logger.LogWarning(
                        "Skipped {OldEmail}: target username {NewEmail} already belongs to user id {ExistingUserId}.",
                        user.Email, newEmail, existingByName.Id);
                    continue;
                }

                var oldEmail = user.Email;
                var oldUserName = user.UserName;

                user.Email = newEmail;
                user.UserName = newEmail;

                await _userManager.UpdateNormalizedEmailAsync(user);
                await _userManager.UpdateNormalizedUserNameAsync(user);

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    failed++;
                    var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    _logger.LogError(
                        "Failed updating user id {UserId} from {OldEmail} to {NewEmail}. Errors: {Errors}",
                        user.Id, oldEmail, newEmail, errors);
                    continue;
                }

                updated++;
                _logger.LogInformation(
                    "Updated user id {UserId}: UserName {OldUserName} -> {NewUserName}; Email {OldEmail} -> {NewEmail}",
                    user.Id, oldUserName, user.UserName, oldEmail, user.Email);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Unexpected error migrating user snapshot id {UserId}.", snapshot.Id);
            }
        }

        _logger.LogInformation(
            "Email domain migration complete. Updated={Updated}, Skipped={Skipped}, Failed={Failed}",
            updated, skipped, failed);
    }
}