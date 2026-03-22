
namespace Store.Application.Common;

public class Result<T>
{
    // all properties are with get only which it will be readonly and set the value only through constructor of the factory methods 
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string Error { get; } = string.Empty;
    public int StatusCode { get; }

    private Result(bool isSuccess, T? value, string error, int statusCode)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    //this is the factory method which is the only way to create Results
    public static Result<T> Success(T value) => new(true, value, string.Empty, 200);
    public static Result<T> NotFound(string error) => new(false, default, error, 404);
    public static Result<T> BadRequest(string error) => new(false, default, error, 400);
    public static Result<T> BadRequest(string error,T value) => new(false, value, error, 400);
    public static Result<T> ServerError(string error) => new(false, default, error, 500);
    public static Result<T> Conflict(string error) => new(false, default, error, 409);
}

//this is a non-Generic version of the Result which used with result with no data
public class Result
{
    // all properties are with get only which it will be readonly and set the value only through constructor of the factory methods 
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; } = string.Empty;
    public int StatusCode { get; }

    private Result(bool isSuccess, string error, int statusCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    //this is the factory method which is the only way to create Results
    public static Result Success() => new(true, string.Empty, 200);
    public static Result NotFound(string error) => new(false, error, 404);
    public static Result BadRequest(string error) => new(false, error, 400);
    public static Result ServerError(string error) => new(false, error, 500);
    public static Result Conflict(string error) => new(false, error, 409);
}
