namespace BlueBrick.Relay;

public sealed class ExecutionBoardOptions
{
    public bool Enabled { get; set; }
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
