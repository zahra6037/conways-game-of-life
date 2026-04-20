namespace GameOfLife.Domain.Entities;

public sealed class Board: IEquatable<Board>
{
    public int Rows { get; }
    public int Cols { get; }
    public int Generation { get; }
    private readonly bool[,] _cells;

    public Board(int rows, int cols, bool[,] cells, int generation = 0)
    {
        if(rows <= 0) throw new ArgumentException("Rows must be positive.", nameof(rows));
        if(cols <= 0) throw new ArgumentException("Columns must be positive.", nameof(cols));
        //logical?
        if(generation < 0) throw new ArgumentException("Generation must be non-negative", nameof(generation));
        if(cells.Length != rows*cols) 
            throw new ArgumentException($"Cells array length must be {rows * cols}",  nameof(cells));
        
        Rows = rows;
        Cols = cols;
        Generation = generation;
        _cells = new bool[rows, cols];
        
        for(int i = 0; i<cells.Length; i++)
        {
            int row = i / cols;
            int col = i % cols;
            _cells[row, col] = cells[i];
        }
    }
    
    public bool Equals(Board? other)
    {
        if (other is null) return false;
        if (Rows != other.Rows || Cols != other.Cols) return false;
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
            if (_cells[r, c] != other._cells[r, c]) return false;
        return true;
    }
    public override bool Equals(object? obj) => Equals(obj as Board);
    
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Rows);
        hash.Add(Cols);
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
            hash.Add(_cells[r, c]);
        return hash.ToHashCode();
    }
    
    public bool IsAlive(int row, int col)
    {
        if (row < 0 || row >= Rows || col < 0 || col >= Cols)
            return false;
        
        return _cells[row, col];
    }
    
    public bool[] GetCells()
    {
        var cells = new bool[Rows * Cols];
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                cells[row * Cols + col] = _cells[row, col];
            }
        }
        return cells;
    }
    
    public Board WithCell(int row, int col, bool alive)
    {
        if (row < 0 || row >= Rows || col < 0 || col >= Cols)
            throw new ArgumentOutOfRangeException($"Invalid position: ({row}, {col})");

        var cells = GetCells();
        cells[row * Cols + col] = alive;
        return new Board(Rows, Cols, cells, Generation);
    }
}