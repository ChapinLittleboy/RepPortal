using System.Reflection;
using Microsoft.Extensions.Logging;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class IdoServiceTests
{
    [Fact]
    public void ConvertTo_ShouldReturnNull_ForNullableIntWhenInputIsBlank()
    {
        var result = InvokeConvertTo("", typeof(int?));

        Assert.Null(result);
    }

    [Fact]
    public void ConvertTo_ShouldParseDecimal_UsingInvariantCulture()
    {
        var result = InvokeConvertTo("123.45", typeof(decimal));

        Assert.Equal(123.45m, Assert.IsType<decimal>(result));
    }

    [Fact]
    public void ConvertTo_ShouldParseDateTime_FromSupportedFormat()
    {
        var result = InvokeConvertTo("20260402", typeof(DateTime));

        Assert.Equal(new DateTime(2026, 4, 2), Assert.IsType<DateTime>(result));
    }

    [Fact]
    public void ConvertTo_ShouldThrowFormatException_ForInvalidInt()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => InvokeConvertTo("not-an-int", typeof(int)));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void MapRow_ShouldMapCsiFields_CaseInsensitively()
    {
        var row = new List<MgNameValue>
        {
            new() { Name = "invnum", Value = "INV-1" },
            new() { Name = "qtyinvoiced", Value = "2" },
            new() { Name = "price", Value = "7.5" },
            new() { Name = "item", Value = "ABC" }
        };

        var result = InvokeMapRow<InvoiceRptDetail>(row);

        Assert.Equal("INV-1", result.InvNum);
        Assert.Equal(2m, result.InvQty);
        Assert.Equal(7.5m, result.Price);
        Assert.Equal("ABC", result.Item);
    }

    [Fact]
    public void Eq_ShouldEscapeSingleQuotes()
    {
        var result = (string)ReflectionTestHelper.InvokeNonPublicStatic(typeof(IdoService), "Eq", "CustNum", "O'Brien")!;

        Assert.Equal("CustNum = 'O''Brien'", result);
    }

    [Fact]
    public void In_ShouldSkipBlankValues()
    {
        var result = (string)ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(IdoService),
            "In",
            "SalesRegion",
            new[] { "NE", "", "SE" })!;

        Assert.Equal("SalesRegion IN ('NE','SE')", result);
    }

    [Fact]
    public void DateGt_ShouldFormatDateAsYyyyMMdd()
    {
        var result = (string)ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(IdoService),
            "DateGt",
            "InvDate",
            new DateTime(2026, 4, 2))!;

        Assert.Equal("InvDate > '20260402'", result);
    }

    private static object? InvokeConvertTo(string? raw, Type targetType)
        => ReflectionTestHelper.InvokeNonPublicStatic(typeof(IdoService), "ConvertTo", raw, targetType);

    private static T InvokeMapRow<T>(List<MgNameValue> row)
        where T : new()
    {
        var method = typeof(IdoService)
            .GetMethod("MapRow", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(T));

        return (T)method.Invoke(null, new object[] { row })!;
    }
}
