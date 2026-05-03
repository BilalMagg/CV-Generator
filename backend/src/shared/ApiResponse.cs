namespace CVGenerator.Shared;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public object? Errors { get; set; }

    public static ApiResponse<T> Ok(T? data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Created(T? data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message ?? "Created successfully"
    };

    public static ApiResponse<T> Error(string message, object? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors
    };
}
