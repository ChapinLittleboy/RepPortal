// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using RepPortal.Data;
using RepPortal.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;

namespace RepPortal.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly SalesService _salesService;
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            SalesService salesService,
            IDbConnectionFactory dbConnectionFactory)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _salesService = salesService;

            RegionOptions = new List<SelectListItem>();
            _dbConnectionFactory = dbConnectionFactory;
        }

        public List<SelectListItem> RegionOptions { get; set; }


        public List<RegistrationCodeInfo> ActiveRegistrationCodes { get; set; } = new();

        private async Task LoadActiveRegistrationCodesAsync()
        {
            const string sql = @"
        SELECT RegistrationCode, RepCode
        FROM AgencyRegistrationCodes
        WHERE IsActive = 1
          AND (ExpirationDate IS NULL OR ExpirationDate > GETUTCDATE())";

            using var connection = _dbConnectionFactory.CreateRepConnection();
           

            ActiveRegistrationCodes = (await connection.QueryAsync<RegistrationCodeInfo>(sql)).ToList();
        }





        private async Task LoadOptionsAsync()
        {
            var regions = await _salesService.GetAllRegionsAsync();
            RegionOptions = regions.Select(r => new SelectListItem { Value = r.Region, Text = r.RegionName }).ToList();
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            [Required]
            [Display(Name = "Registration Code")]
            public string RegistrationCode { get; set; }

            [Display(Name = "Region")]
            public string Region { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            await LoadOptionsAsync();
            await LoadActiveRegistrationCodesAsync();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
            {
                await LoadOptionsAsync();
                return Page();
            }

            // Validate the RegistrationCode, get RepCode
            string repCode = await ValidateRegistrationCodeAsync(Input.RegistrationCode);
            if (repCode == null)
            {
                ModelState.AddModelError("Input.RegistrationCode", "Invalid, expired, or inactive registration code.");
                await LoadOptionsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Email is required.");
                await LoadOptionsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Input.Password))
            {
                ModelState.AddModelError("Input.Password", "Password is required.");
                await LoadOptionsAsync();
                return Page();
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                // Set the RepCode found from RegistrationCode
                user.RepCode = repCode;
                user.FirstName = Input.FirstName;
                user.LastName = Input.LastName;
                user.Region = Input.Region;

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var internalDomains = new[] { "@chapinmfg.com", "@heathmfg.com", "@ChapinCustomMolding.com" };

                    string role = internalDomains.Any(d => Input.Email.EndsWith(d, StringComparison.OrdinalIgnoreCase))
                        ? "User"
                        : "SalesRep";

                    var roleResult = await _userManager.AddToRoleAsync(user, role);

                    if (!roleResult.Succeeded)
                    {
                        foreach (var error in roleResult.Errors)
                        {
                            _logger.LogError("Error assigning role: {Error}", error.Description);
                            ModelState.AddModelError(string.Empty, $"Role assignment failed: {error.Description}");
                            return Page();
                        }
                    }

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            await LoadOptionsAsync();
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                var user = Activator.CreateInstance<ApplicationUser>();

                // Set properties in OnPostAsync instead to ensure we have RepCode set properly

                return user;
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }

        /// <summary>
        /// Validates the registration code against the AgencyRegistrationCodes table.
        /// Returns the RepCode if valid, otherwise null.
        /// </summary>
        private async Task<string> ValidateRegistrationCodeAsync(string registrationCode)
        {
            if (string.IsNullOrWhiteSpace(registrationCode))
                return null;

            // Normalize input
            registrationCode = registrationCode.Trim();

            // Assuming _salesService has a method to validate the code.
            // If not, you can do a direct EF query here if you inject DbContext.

            var repCode = await _salesService.GetRepCodeByRegistrationCodeAsync(registrationCode);

            return repCode;
        }
    }

    public class RegistrationCodeInfo
    {
        public string RegistrationCode { get; set; }
        public string RepCode { get; set; }
    }
}

