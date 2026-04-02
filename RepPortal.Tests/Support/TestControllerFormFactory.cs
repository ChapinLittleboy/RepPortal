using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace RepPortal.Tests.Support;

internal static class TestControllerFormFactory
{
    public static void SetForm(HttpContext httpContext, Dictionary<string, Microsoft.Extensions.Primitives.StringValues>? fields = null, FormFileCollection? files = null)
    {
        var form = new FormCollection(fields ?? new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files ?? new FormFileCollection());
        httpContext.Features.Set<IFormFeature>(new FormFeature(form));
    }

    public static IFormFile CreateFile(string fileName, string content, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
