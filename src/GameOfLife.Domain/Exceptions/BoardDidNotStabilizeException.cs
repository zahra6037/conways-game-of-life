namespace GameOfLife.Domain.Exceptions;

public sealed class BoardDidNotStabilizeException : Exception
{
    public string BoardId { get; }
    public int MaxIterations { get; }

    public BoardDidNotStabilizeException(string boardId, int maxIterations)
        : base($"Board '{boardId}' did not reach a stable state within {maxIterations} iterations.")
    {
        BoardId = boardId;
        MaxIterations = maxIterations;
    }
}