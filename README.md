# Conway's Game of Life API

A .NET 8 Web API implementing Conway's Game of Life using clean architecture with SQLite database persistence.

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Installing .NET 8 SDK on macOS
```bash
# Option 1: Using Homebrew (recommended)
brew install dotnet

# Option 2: Download from Microsoft
# Visit https://dotnet.microsoft.com/download/dotnet/8.0
# Download the macOS installer and run it
```

Verify installation:
```bash
dotnet --version
# Should show 8.0.x or higher
```

### Running the API
```bash
# Navigate to the project directory
cd ~/conways-game-of-life

# Restore dependencies
dotnet restore

# Run the API
cd src/GameOfLife.API
dotnet run
```

The API will be available at:
- **HTTP**: `http://localhost:5000`
- **Swagger UI**: `http://localhost:5000/swagger` (in development mode)

## API Endpoints

> A ready-to-import **Postman collection** lives at [`docs/GameOfLife.postman_collection.json`](docs/GameOfLife.postman_collection.json). Import it into Postman, set the `baseUrl` collection variable (default `http://localhost:5000`), and run **Upload Board** first — the `boardId` is captured automatically and reused by the other requests.
>
> **Architecture diagram**: [`docs/architecture.excalidraw`](docs/architecture.excalidraw) — drag onto <https://excalidraw.com> to view.
>

Cells are encoded as integers: `1` = alive, `0` = dead.

### 1. Upload Board State
```http
POST /api/boards
Content-Type: application/json

{
  "grid": [
    [1, 1],
    [1, 1]
  ]
}
```
**Response** `201 Created`:
```json
{
  "boardId": "board_1fdbb28c3b964cf681037c5104192ffd_1776384755415",
  "initialState": [[1, 1], [1, 1]],
  "generation": 0
}
```

### 2. Get Next State
```http
GET /api/boards/{boardId}/next
```
Returns the board after applying Conway's rules once.

### 3. Get N States Ahead
```http
GET /api/boards/{boardId}/states/{n}
```
Returns the board after `n` generations.

### 4. Get Final State
```http
GET /api/boards/{boardId}/final
```
Runs the simulation until the board reaches a still life or a previously-seen state (oscillator) and returns it. The `generation` field on the response indicates how many iterations it took. The server runs up to a hard ceiling of **100,000** generations; if the board still hasn't stabilized, responds with `422 Unprocessable Entity` and an RFC 7807 problem document.

### Get Stored Board
```http
GET /api/boards/{boardId}
```
Returns the originally uploaded state.

All non-2xx responses are returned as RFC 7807 `application/problem+json`
documents with `type`, `title`, `status`, `detail` and `instance` fields.

## Conway's Game of Life Rules

1. **Survival**: Live cell with 2-3 neighbors survives
2. **Birth**: Dead cell with exactly 3 neighbors becomes alive
3. **Death**: All other cells die or remain dead

## Architecture

Built using **Clean Architecture** principles:

```
├── GameOfLife.Domain/          # Core business logic
│   ├── Entities/Board.cs       # Immutable board entity
│   ├── Services/GameRulesService.cs  # Conway's rules
│   └── Exceptions/            # Domain exceptions
├── GameOfLife.Application/     # Use cases
│   ├── Ports/                 # Interfaces
│   └── Services/GameService.cs # Business operations
├── GameOfLife.Infrastructure/  # External concerns
│   └── Persistence/SqliteBoardRepository.cs # SQLite storage
└── GameOfLife.API/            # HTTP interface
    ├── Controllers/           # REST endpoints
    ├── Models/               # DTOs
    └── ExceptionHandlers/    # Error handling
```

## Data Storage

- **Database**: SQLite (`gameoflife.db`)
- **Schema**: Single `Boards` table with JSON serialized cell data
- **Auto-created**: Database and table created automatically on first run

## Logging & Tracing

The API emits structured logs via the default ASP.NET Core console provider, plus OpenTelemetry trace spans for both incoming HTTP requests and the domain operations (`CreateBoard`, `GetNextState`, `GetStateAhead`, `GetFinalState`). Spans are exported to a single local JSON file (`logs/traces.json`) in **Jaeger's upload format** via a custom `FileTraceExporter`, so you can drop the file straight into the Jaeger UI to inspect traces.

Configure via `appsettings.json`:
```json
"Telemetry": {
  "ServiceName": "GameOfLife.API",
  "TraceFilePath": "logs/traces.json"
}
```

The path is resolved relative to the process working directory, so running `dotnet run` from `src/GameOfLife.API` puts the file at `src/GameOfLife.API/logs/traces.json`. The file is atomically rewritten on every flush (every ~5s while the process runs, plus once on shutdown), so it always represents a complete, valid JSON document.

### Viewing in Jaeger UI
1. Open the Jaeger UI (e.g. <https://www.jaegertracing.io/docs/latest/frontend-ui/> demo, or your own instance).
2. On the Search page, use the **JSON File** upload control and select `logs/traces.json`.
3. The trace list will populate; click any entry to see the full waterfall (HTTP server span as parent, domain spans as children, with all tags: `board.id`, `board.rows`, `board.cols`, `http.response.status_code`, etc.).

## Example Usage

### 1. Create a Blinker Pattern
```bash
curl -X POST "http://localhost:5000/api/boards" \
  -H "Content-Type: application/json" \
  -d '{
    "grid": [
      [0, 1, 0],
      [0, 1, 0],
      [0, 1, 0]
    ]
  }'
```

### 2. Watch It Oscillate
```bash
# Get next state (horizontal line)
curl "http://localhost:5000/api/boards/{boardId}/next"

# Get 4 generations ahead
curl "http://localhost:5000/api/boards/{boardId}/states/4"

# Detect the cycle and get the final stable state
curl "http://localhost:5000/api/boards/{boardId}/final"
```

## Development

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Project Structure
```
GameOfLife/
├── GameOfLife.sln
├── .github/workflows/copilot-setup-steps.yml
└── src/
    ├── GameOfLife.Domain/
    ├── GameOfLife.Application/
    ├── GameOfLife.Infrastructure/
    └── GameOfLife.API/
```

## Features

- **Clean Architecture** with proper separation of concerns
- **SQLite Persistence** with automatic database creation
- **Immutable Domain Model** with validation
- **RESTful API** following HTTP standards
- **Error Handling** with RFC 9110 ProblemDetails
- **Swagger Documentation** for easy testing
- **Dependency Injection** throughout all layers
- **JSON Serialization** for board state storage

## Response Format

State responses follow this structure:
```json
{
  "boardId": "board_1fdbb28c3b964cf681037c5104192ffd_1776384755415",
  "state": [
    [0, 1, 0],
    [0, 1, 0],
    [0, 1, 0]
  ],
  "generation": 3
}
```

## Error Responses

Missing board returns `404 Not Found` with ProblemDetails:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Board Not Found", 
  "status": 404,
  "detail": "Board with id {guid} was not found.",
  "instance": "/api/boards/{guid}/next",
  "boardId": "{guid}"
}
```

---
## Agent Skills

This repository includes agent skill instructions under `.github/skills/`.

If you are using an AI coding agent, it should load these skills automatically when working in this repo:

- `.github/skills/debugging/SKILL.md` — guidance for diagnosing API, validation, persistence, and HTTP contract issues
- `.github/skills/testing/SKILL.md` — guidance for writing and running tests for the API, application, and domain layers

These skills are intended to help agents follow the project’s conventions and troubleshoot issues consistently.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
