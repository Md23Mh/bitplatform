﻿
namespace Boilerplate.Shared.Dtos.Identity;

public class IdentityRequestDto : IValidatableObject
{
    /// <example>bitplatform</example>
    [Display(Name = nameof(AppStrings.UserName))]
    public virtual string? UserName { get; set; }

    /// <example>test@bitplatform.dev</example>
    [EmailAddress(ErrorMessage = nameof(AppStrings.EmailAddressAttribute_ValidationError))]
    [Display(Name = nameof(AppStrings.Email))]
    public virtual string? Email { get; set; }

    /// <example>+31684207362</example>
    [Phone(ErrorMessage = nameof(AppStrings.PhoneNumber))]
    [Display(Name = nameof(AppStrings.PhoneAttribute_Invalid))]
    public virtual string? PhoneNumber { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(UserName) && string.IsNullOrEmpty(Email) && string.IsNullOrEmpty(PhoneNumber))
            yield return new ValidationResult(errorMessage: nameof(AppStrings.EitherProvideUserNameOrEmailOrPhoneNumber), [nameof(Email), nameof(PhoneNumber)]);
    }
}