using System.Text.RegularExpressions;

/// <summary>
/// Thanks GPT4
/// </summary>
public class EmailValidator
{
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            // This regex pattern generally covers most email formats.
            // Note that perfect email validation via regex is nearly impossible due to the complexity of the spec.
            // But for most practical purposes, this pattern will suffice.
            string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

            return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}