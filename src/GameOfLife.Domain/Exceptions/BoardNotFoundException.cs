namespace GameOfLife.Domain.Exceptions;

public class BoardNotFoundException : Exception
{
    public string BoardId { get; }
    public BoardNotFoundException(string boardId) 
        : base($"Board with id '{boardId}' was not found.")
    {
        BoardId = boardId;
    }
}