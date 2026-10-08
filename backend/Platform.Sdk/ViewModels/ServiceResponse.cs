namespace Primworth_API.ViewModels
{
    /// <summary>The platform's response envelope. Every endpoint returns one.</summary>
    public class ServiceResponse<T>
    {
        public T Data { get; set; } = default!;
        public string Message { get; set; } = "";
        public bool Success { get; set; }
    }
}
