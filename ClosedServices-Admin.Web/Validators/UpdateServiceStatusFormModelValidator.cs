using ClosedServices_Admin.Components.Pages.Services;
using FluentValidation;

namespace ClosedServices_Admin.Validators
{
    public sealed class UpdateServiceStatusFormModelValidator : AbstractValidator<UpdateServiceStatusFormModel>
    {
        public UpdateServiceStatusFormModelValidator()
        {
            RuleFor(form => form.StartDate)
                .NotNull()
                .When(form => form.SelectedDuration == "custom")
                .WithMessage("You must enter a start date if you've chosen a custom closure date.");

            RuleFor(form => form.EndDate)
                .Must((form, endDate) => !endDate.HasValue || !form.StartDate.HasValue || endDate.Value >= form.StartDate.Value)
                .When(form => form.SelectedDuration == "custom")
                .WithMessage("The custom end date must be the same as or after the custom start date.");

            RuleFor(form => form)
                .Must(HaveValidCustomEndDateTime)
                .When(form => form.SelectedDuration == "custom" && form.StartDate.HasValue && form.EndDate == form.StartDate)
                .OverridePropertyName(nameof(UpdateServiceStatusFormModel.EndDate))
                .WithMessage("The end date and time must be after the start date and time.");

            RuleFor(form => form.Message)
                .MaximumLength(1000)
                .WithMessage("Message must be 1000 characters or fewer.");
        }

        private static bool HaveValidCustomEndDateTime(UpdateServiceStatusFormModel form)
        {
            if (!form.StartDate.HasValue || !form.EndDate.HasValue)
            {
                return true;
            }

            var startDateTime = form.StartDate.Value.ToDateTime(form.StartTime ?? TimeOnly.MinValue);
            var endDateTime = form.EndDate.Value.ToDateTime(form.EndTime ?? TimeOnly.MaxValue);
            return endDateTime > startDateTime;
        }
    }
}
