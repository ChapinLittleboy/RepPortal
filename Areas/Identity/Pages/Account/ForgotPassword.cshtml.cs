// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using RepPortal.Data;

namespace RepPortal.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private const string OldCompanyDomain = "chapinmfg.com";
        private const string NewCompanyDomain = "chapinusa.com";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await FindUserByEmailOrCompanyAliasAsync(Input.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Do not reveal whether an account exists or is confirmed.
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            var email = await _userManager.GetEmailAsync(user);
            await _emailSender.SendEmailAsync(
                email,
                "Reset Password",
                $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            return RedirectToPage("./ForgotPasswordConfirmation");
        }

        private async Task<ApplicationUser> FindUserByEmailOrCompanyAliasAsync(string email)
        {
            foreach (var candidate in GetCompanyEmailLookupCandidates(email))
            {
                var user = await _userManager.FindByEmailAsync(candidate);
                if (user != null)
                {
                    return user;
                }
            }

            return null;
        }

        internal static IReadOnlyList<string> GetCompanyEmailLookupCandidates(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Array.Empty<string>();
            }

            email = email.Trim();
            var candidates = new List<string> { email };

            var atIndex = email.IndexOf('@');
            if (atIndex < 0)
            {
                return candidates;
            }

            var localPart = email.Substring(0, atIndex);
            var domain = email.Substring(atIndex + 1);
            var aliasDomain = domain.Equals(OldCompanyDomain, StringComparison.OrdinalIgnoreCase)
                ? NewCompanyDomain
                : domain.Equals(NewCompanyDomain, StringComparison.OrdinalIgnoreCase)
                    ? OldCompanyDomain
                    : null;

            if (aliasDomain != null)
            {
                var alias = $"{localPart}@{aliasDomain}";
                if (!candidates.Contains(alias, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(alias);
                }
            }

            return candidates;
        }
    }
}
