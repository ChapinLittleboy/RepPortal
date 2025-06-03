using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using RepPortal.Data;
using RepPortal.Models;
using RepPortal.Services;
using Syncfusion.Blazor.Grids;

namespace RepPortal.Pages
{
    [Authorize]
    public partial class CustomerList : ComponentBase
    {
        [Inject] protected AuthenticationStateProvider AuthenticationStateProvider { get; set; }
        [Inject] protected UserManager<ApplicationUser> UserManager { get; set; }
        [Inject] protected CustomerService CustomerService { get; set; }
        [Inject] protected IRepCodeContext RepCodeContext { get; set; }
        [Inject] protected TitleService TitleService { get; set; }
        [Inject] protected RepPortal.Services.IActivityLogService ActivityLogService { get; set; }

        private IEnumerable<Customer> customers;
        private string repCode;
        private bool isLoading = true;
        private SfGrid<Customer> Grid;
        //private List<CreditHoldReasonCode> reasonCodeList = new List<CreditHoldReasonCode>();
        public string _state;
        private List<CreditHoldReasonCode> _creditHoldReasons = new();
        private Dictionary<string, string> _reasonLookup = new();
        private IEnumerable<CustType> CustomerTypesList;

        protected override async Task OnInitializedAsync()
        {
            // Activity Logging: Log report usage to ReportUsageActivity
            await ActivityLogService.LogReportUsageActivityAsync("Customer List Report", "");

            //reasonCodeList = await CustomerService.GetAllReasonCodesAsync();
            // _creditHoldReasons = await CustomerService.GetAllReasonCodesAsync();
            // _reasonLookup = _creditHoldReasons
            //     .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            //     .ToDictionary(r => r.Code, r => r.Description);

            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            repCode = user.FindFirst("RepCode")?.Value;
            CustomerTypesList = await CustomerService.GetCustomerTypesListAsync();

            repCode = RepCodeContext.CurrentRepCode;
            if (user.Identity.IsAuthenticated)
            {
                var currentUser = await UserManager.GetUserAsync(user);
                if (!string.IsNullOrEmpty(repCode))
                {
                    var allowedTypes = new HashSet<string>(
                        CustomerTypesList.Select(ct => ct.CustomerType),
                        StringComparer.OrdinalIgnoreCase // Case-insensitive comparison
                    );

                    var allCustomers = await CustomerService.GetCustomerNamesByRepCodeAsync();
                    foreach (var cust in allCustomers)
                    {
                        if (!allowedTypes.Contains(cust.BuyingGroup))
                        {
                            cust.BuyingGroup = null; // or string.Empty if you prefer
                            cust.BuyingGroupDescription = null; // or string.Empty if you prefer
                        }
                    }
                    customers = allCustomers.Where(c => c.Status != "R");

                    isLoading = false;
                }
            }

            await base.OnInitializedAsync();
        }

        private async Task ClearFilters()
        {
            await Grid.ClearFilteringAsync();
        }

        private string GetHoldReasonDescription(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            return _reasonLookup.TryGetValue(code, out var desc) ? desc : code;
        }

        private string GetStatusDescription(string status)
        {
            if (Enum.TryParse<Customer.CustomerStatus>(status, out var customerStatus))
            {
                return customerStatus.ToDescription();
            }
            return "Unknown";
        }

        private string YesNoAccessor(object data, string field)
        {
            var isActive = (int)data.GetType().GetProperty(field).GetValue(data, null);
            return isActive == 1 ? "Yes" : "No";
        }

        private List<CreditHoldLookup> creditHoldLookup = new()
        {
            new CreditHoldLookup { Id = 1, Text = "Yes" },
            new CreditHoldLookup { Id = 0, Text = "No" }
        };

        public class CreditHoldLookup
        {
            public int Id { get; set; }
            public string Text { get; set; }
        }

        public async Task ToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
        {
            if (args.Item.Id == "Grid_Export to Excel") //Id is combination of Grid's ID and itemname
            {
                var ExcelFileName = $"Customers Report({RepCodeContext.CurrentRepCode}).xlsx";
                ExcelExportProperties exportProperties = new ExcelExportProperties
                {
                    FileName = ExcelFileName
                };
                await this.Grid.ExportToExcelAsync(exportProperties);
            }

            if (args.Item.Id == "Grid_Save Layout") //Id is combination of Grid's ID and itemname
            {
                // Add layout save logic here if needed
            }
        }

        public class CreditHoldDateComparer : IComparer<object>
        {
            public int Compare(object XRowDataToCompare, object YRowDataToCompare)
            {
                var xCust = XRowDataToCompare as Customer;
                var yCust = YRowDataToCompare as Customer;

                // grab the underlying nullable DateTime
                DateTime? xDate = xCust?.CreditHoldDate;
                DateTime? yDate = yCust?.CreditHoldDate;

                // both null → equal
                if (!xDate.HasValue && !yDate.HasValue)
                    return 0;

                // only X is null → send X after Y
                if (!xDate.HasValue)
                    return 1;

                // only Y is null → send X before Y
                if (!yDate.HasValue)
                    return -1;

                // both have values → compare normally
                return xDate.Value.CompareTo(yDate.Value);
            }
        }
    }
}
