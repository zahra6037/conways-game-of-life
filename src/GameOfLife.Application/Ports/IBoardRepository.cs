namespace GameOfLife.Application.Ports;

public interface IBoardRepository
{
    Task<Board?> GetAsync(string id, CancellationToken ct =  default);
    Task SaveAsync(string id, Board board, CancellationToken ct = default );
    Task<bool> DeleteAsync(string id, CancellationToken cd = default );
}