namespace SmsService.Application;

/// <summary>Business error that the API turns into a ProblemDetails response with a "code" field.</summary>
public class AppException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AppException(string code, string message, int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public class AppValidationException : AppException
{
    public AppValidationException(string code, string message) : base(code, message, 400) { }
}

public class AppNotFoundException : AppException
{
    public AppNotFoundException(string code, string message) : base(code, message, 404) { }
}
