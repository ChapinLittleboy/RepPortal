using Microsoft.Data.SqlClient;

namespace Microsoft.Extensions.Configuration;

public static class ConfigurationSecretExtensions
{
    public static string GetRequiredResolvedConnectionString(this IConfiguration configuration, string name)
    {
        var rawConnectionString = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException($"Connection string '{name}' not found.");
        }

        return configuration.ResolveConnectionString(rawConnectionString, $"ConnectionStrings:{name}");
    }

    public static string ResolveConnectionString(
        this IConfiguration configuration,
        string rawConnectionString,
        string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException($"Configuration value '{configurationPath}' is missing.");
        }

        var builder = new SqlConnectionStringBuilder(rawConnectionString);
        if (!string.IsNullOrWhiteSpace(builder.Password))
        {
            return builder.ConnectionString;
        }

        var passwordVariable = GetPasswordVariableName(builder.UserID);
        if (passwordVariable is null)
        {
            return builder.ConnectionString;
        }

        var password = configuration[passwordVariable] ?? Environment.GetEnvironmentVariable(passwordVariable);
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"Configuration value '{configurationPath}' requires environment variable '{passwordVariable}'.");
        }

        builder.Password = password;
        return builder.ConnectionString;
    }

    public static string? GetSmtpPassword(this IConfiguration configuration)
    {
        return configuration["Smtp:Password"]
               ?? configuration["SmtpSettings:Password"]
               ?? configuration["ADMIN_PWD"]
               ?? Environment.GetEnvironmentVariable("ADMIN_PWD");
    }

    private static string? GetPasswordVariableName(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (string.Equals(userId, "sa", StringComparison.OrdinalIgnoreCase))
        {
            return "SA_PWD";
        }

        if (string.Equals(userId, "ReportUser1", StringComparison.OrdinalIgnoreCase))
        {
            return "ReportUser1_PWD";
        }

        return null;
    }
}
