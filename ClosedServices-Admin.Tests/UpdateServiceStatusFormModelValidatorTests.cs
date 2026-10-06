using ClosedServices_Admin.Components.Pages.Services;
using ClosedServices_Admin.Validators;

namespace ClosedServices_Admin.Tests;

public class UpdateServiceStatusFormModelValidatorTests
{
    private readonly UpdateServiceStatusFormModelValidator sut = new();

    [Fact]
    public void Validate_CustomDurationWithoutStartDate_IsInvalid()
    {
        var model = new UpdateServiceStatusFormModel
        {
            SelectedDuration = "custom",
            StartDate = null
        };

        var result = sut.Validate(model);

        var startDateError = Assert.Single(result.Errors, error => error.PropertyName == nameof(UpdateServiceStatusFormModel.StartDate));
        Assert.Equal("You must enter a start date if you've chosen a custom closure date.", startDateError.ErrorMessage);
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_IsInvalid()
    {
        var model = new UpdateServiceStatusFormModel
        {
            SelectedDuration = "custom",
            StartDate = new DateOnly(2025, 3, 15),
            EndDate = new DateOnly(2025, 3, 14)
        };

        var result = sut.Validate(model);

        var endDateError = Assert.Single(result.Errors, error => error.PropertyName == nameof(UpdateServiceStatusFormModel.EndDate));
        Assert.Equal("The custom end date must be the same as or after the custom start date.", endDateError.ErrorMessage);
    }

    [Fact]
    public void Validate_EndDateEqualToStartDate_IsValid()
    {
        var model = new UpdateServiceStatusFormModel
        {
            SelectedDuration = "custom",
            StartDate = new DateOnly(2025, 3, 15),
            EndDate = new DateOnly(2025, 3, 15)
        };

        var result = sut.Validate(model);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EndDateEqualToStartDateWithEarlierEndTime_IsInvalid()
    {
        var model = new UpdateServiceStatusFormModel
        {
            SelectedDuration = "custom",
            StartDate = new DateOnly(2025, 3, 15),
            StartTime = new TimeOnly(14, 0),
            EndDate = new DateOnly(2025, 3, 15),
            EndTime = new TimeOnly(13, 0)
        };

        var result = sut.Validate(model);

        var endDateError = Assert.Single(result.Errors, error => error.PropertyName == nameof(UpdateServiceStatusFormModel.EndDate));
        Assert.Equal("The end date and time must be after the start date and time.", endDateError.ErrorMessage);
    }

    [Fact]
    public void Validate_OpenEndedCustomClosure_IsValid()
    {
        var model = new UpdateServiceStatusFormModel
        {
            SelectedDuration = "custom",
            StartDate = new DateOnly(2025, 3, 15),
            EndDate = null
        };

        var result = sut.Validate(model);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MessageOf1000Characters_IsValid()
    {
        var model = new UpdateServiceStatusFormModel
        {
            SelectedDuration = "custom",
            StartDate = new DateOnly(2025, 3, 15),
            Message = new string('a', 1000)
        };

        var result = sut.Validate(model);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MessageOver1000Characters_IsInvalid()
    {
        var model = new UpdateServiceStatusFormModel
        {
            SelectedDuration = "custom",
            StartDate = new DateOnly(2025, 3, 15),
            Message = new string('a', 1001)
        };

        var result = sut.Validate(model);

        var messageError = Assert.Single(result.Errors, error => error.PropertyName == nameof(UpdateServiceStatusFormModel.Message));
        Assert.Equal("Message must be 1000 characters or fewer.", messageError.ErrorMessage);
    }
}
