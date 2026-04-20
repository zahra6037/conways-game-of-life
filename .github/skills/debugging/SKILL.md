---
name: debugging
description: Diagnose failures in the Conway's Game of Life API — request validation, persistence, cycle detection, and HTTP error contracts.
---

# Debugging skill

## Mental model
Requests flow:

```
HTTP → GameController → GameService → GameRulesService (pure)
                                  └→ IBoardRepository (SqliteBoardRepository in prod)
Errors → IExceptionHandler chain → RFC 7807 ProblemDetails JSON
```

Three exception handlers run in this order (defined in `Program.cs`):
1. `BoardNotFoundExceptionHandler` → **404**
2. `BoardDidNotStabilizeExceptionHandler` → **422**
3. `ValidationExceptionHandler` → **400** (catches `ValidationException` and `ArgumentException`)

If a request returns a 500, none of the above matched — the exception type is the first thing to inspect.

## Common symptoms → where to look

| Symptom | Likely cause | First file to open |
|---|---|---|
| `400 "grid"` error from a payload that looks fine | Cell value isn't strictly `0` or `1`, or grid is ragged | `src/GameOfLife.API/Models/Models.cs` (`UploadBoardRequest.Validate`) |
| `404` immediately after upload | Caller is hitting `/api/boards/{id}` with the wrong id format; `BoardId.IsValid` rejects it before DB lookup | `src/GameOfLife.Domain/Entities/BoardId.cs` |
| `422 "Board Did Not Stabilize"` | Pattern doesn't reach a still life or known cycle within the server's hard ceiling (`GameService.MaxFinalStateIterations` = 100,000) | `src/GameOfLife.Domain/Services/GameRulesService.cs` (`FindFinalState`) |
| Stale data after restart | Connection string points elsewhere or DB file deleted | `appsettings.json` → `ConnectionStrings:GameOfLife` |
| Test passes locally, fails in CI | The test wrote to `gameoflife.db` instead of using `TestApiFactory` | `tests/GameOfLife.API.Tests/GameApiTests.cs` |

## Fast triage commands

```bash
# Build + full test suite
dotnet build && dotnet test

# Run just the failing project
dotnet test tests/GameOfLife.API.Tests --logger "console;verbosity=detailed"

# Run the API locally on a fixed port
ASPNETCORE_URLS=http://localhost:5000 dotnet run --project src/GameOfLife.API

# Reproduce the documented upload contract
curl -s -X POST http://localhost:5000/api/boards \
  -H "Content-Type: application/json" \
  -d '{"grid":[[1,1],[1,1]]}' | jq .

# Inspect the SQLite database (read-only)
sqlite3 src/GameOfLife.API/gameoflife.db "SELECT Id, Rows, Cols, Generation, length(Cells) FROM Boards;"
```

## When `FindFinalState` reports non-stabilization
The algorithm tracks a `HashSet<Board>` of every state seen. It returns when:
- the next state equals the current state (still life), **or**
- the next state has been seen before (any cycle length).

If it throws `BoardDidNotStabilizeException`, the pattern genuinely doesn't repeat within the server's hard ceiling (`GameService.MaxFinalStateIterations` = 100,000). Options:
1. Confirm the pattern isn't a translating spaceship (glider, LWSS) — those don't repeat their exact state, but on a finite grid they typically slide off and the field stabilizes to all-dead, which **is** a still life and **will** be caught.
2. If a test expects stabilization but doesn't see it, double-check the grid was entered row-major and not transposed.
3. If a real use case needs a higher ceiling, raise `GameService.MaxFinalStateIterations` — don't expose it as a query parameter (the contract is "give me the final state", not "try this hard").

## Don't do
- Don't catch `Exception` in controllers — let the handler chain produce the RFC 7807 response.
- Don't rely on `Guid` parsing; board ids are strings shaped like `board_<32hex>_<unix-ms>`.
- Don't touch `gameoflife.db` from tests — always go through `TestApiFactory`.
