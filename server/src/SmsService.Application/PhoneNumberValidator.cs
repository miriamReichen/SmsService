using System.Text.RegularExpressions;

namespace SmsService.Application;

/// <summary>Israeli mobile format: "05" followed by 8 digits (decision 10).</summary>
public static partial class PhoneNumberValidator
{
    [GeneratedRegex(@"^05\d{8}$")]
    private static partial Regex IsraeliMobile();

    public static bool IsValid(string? number) => number is not null && IsraeliMobile().IsMatch(number);
}
