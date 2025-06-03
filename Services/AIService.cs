using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RepPortal.Models;

public class AIService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public AIService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> GetResponseFromAIAsync(string userInput, ModelContext context)
    {
        var model = _config["OpenAI:Model"] ?? "gpt-4o";
        var apiKey = _config["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured.");


        var filters = string.Join(", ", context.DataFilters.Select(kv => $"{kv.Key}: {kv.Value}"));


        var schemaHint = @"
You can retrieve data in two ways:

---

1. **Shipping Data (via SQL)**  
Use T-SQL queries to retrieve monthly or yearly shipping summaries from the following dynamic result set:

SalesData includes these columns:
- Customer (varchar)
- Customer Name (varchar)
- Ship To Num (int)
- Ship To City (varchar)
- Ship To State (varchar)
- slsman (varchar)
- Bill To State (varchar)
- Uf_SalesRegion (varchar)
- RegionName (varchar)
- FY<year> (decimal) – total revenue for that fiscal year
- <MMM><YYYY> (decimal) – monthly revenue, e.g., Sep2024, Oct2024

If the user asks about shipment summaries, respond with a T-SQL query starting with:
SQL: SELECT ...

---

2. **Invoice Data (via method call)**  
To retrieve invoice detail records, use this method call:

**Method: GetInvoiceRptData**  
**Required Parameters**:
  - BeginInvoiceDate (string, format MM/DD/YYYY)
  - EndInvoiceDate (string, format MM/DD/YYYY)
  - RepCode (string)

**Optional Parameter**:
  - CustNum (string or null)

- If the user provides a date range in their question, you MUST extract those dates and place them into the BeginInvoiceDate and EndInvoiceDate parameters.
- Do NOT omit these values — they are required.


When returning invoice data, you **must start** the response with `CALL:` followed by a single valid JSON object.

Example:
User says: ""How much was invoiced from 1/5/2025 to 2/28/2025?""
You respond with:
CALL: {
  ""method"": ""GetInvoiceRptData"",
  ""parameters"": {
    ""BeginInvoiceDate"": ""01/05/2025"",
    ""EndInvoiceDate"": ""02/28/2025"",
    ""RepCode"": ""PRL"",
    ""CustNum"": null
  }
}

---

Only respond in one of these two formats based on the user's question:

- For shipping summaries: return a T-SQL query starting with `SQL:`
- For invoice-related questions: return a JSON object starting with `CALL:`

Do **not** include any commentary, explanation, or preamble text — return only the response in the correct format.
Do not return just a JSON object. Always prepend `CALL:` exactly like in the example above.



When displaying a table, use proper markdown table syntax with:
- a header row
- a separator line of hyphens
- aligned columns
- one row per invoice
";





        var messages = new[]
{
    new
    {
        role = "system",
        content = $"You are assisting a user on the {context.Page} page.\n" +
                  $"User: {context.User}, Role: {context.Role}\n" +
                  $"Filters: {filters}\n" +
                  $"Summary: {context.Summary}\n\n" +
                  schemaHint
    },
    new { role = "user", content = userInput }
};


        var body = new
        {
            model = model,
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI API error ({response.StatusCode}): {errorText}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? "No response from AI.";
    }

    public async Task<string> SendFollowupRequestAsync(object[] messages)
    {
        var model = _config["OpenAI:Model"] ?? "gpt-4o";
        var apiKey = _config["OpenAI:ApiKey"];

        var body = new
        {
            model = model,
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }
}
