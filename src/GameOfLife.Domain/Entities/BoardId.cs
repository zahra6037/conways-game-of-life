namespace GameOfLife.Domain.Entities;

public static class BoardId
{
    private const string Prefix = "board_";

    public static string New() =>
        $"{Prefix}{Guid.NewGuid():N}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    public static bool IsValid(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.StartsWith(Prefix, StringComparison.Ordinal) &&
        id.Length <= 128;
}