using InventoryErp.Application.Common;

namespace InventoryErp.Application.Tests.Common;

public class ServiceResultTests
{
    [Fact]
    public void Success_carries_the_payload()
    {
        var result = ServiceResult<string>.Success("abc");

        Assert.True(result.IsSuccess);
        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal("abc", result.Data);
        Assert.Null(result.Error);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void NotFound_is_a_failure_with_no_payload()
    {
        var result = ServiceResult<string>.NotFound("missing");

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Null(result.Data);
        Assert.Equal("missing", result.Error);
    }

    [Fact]
    public void Invalid_collects_the_validation_errors()
    {
        var result = ServiceResult<string>.Invalid("SKU is required.", "Name is required.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.Equal(2, result.ValidationErrors.Count);
        Assert.Contains("SKU is required.", result.ValidationErrors);
    }

    [Fact]
    public void Conflict_reports_the_conflict_status()
    {
        var result = ServiceResult<string>.Conflict("duplicate");

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("duplicate", result.Error);
    }

    [Fact]
    public void Implicit_conversion_from_a_value_produces_success()
    {
        ServiceResult<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Data);
    }

    [Fact]
    public void Non_generic_success_has_no_error()
    {
        var result = ServiceResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }
}
