namespace GameOfLife.Domain.Exceptions;

public class BoardNotFoundException
{
    public string BoardId { get; }
    public BoardNotFoundException(string boardId) 
        : base($"Board with id '{boardId}' was not found.")
    {
        BoardId = boardId;
    }
}