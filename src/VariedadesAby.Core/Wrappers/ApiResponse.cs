namespace VariedadesAby.Core.Wrappers;

/// <summary>
/// Wrapper genérico para respuestas de la API
/// </summary>
public record ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<string>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Operación exitosa")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null)
        => new() { Success = false, Message = message, Errors = errors };
}
