---
name: testing
description: Run, write, and triage tests for the Conway's Game of Life API (xUnit, .NET 8).
---

# Testing skill

## Layout
The solution has three test projects under `tests/`:

| Project | Scope |
|---|---|
| `GameOfLife.Domain.Tests` | Pure domain logic: `Board`, `BoardId`, `GameRulesService` (Conway rules + cycle/still-life detection). |
| `GameOfLife.Application.Tests` | `GameService` orchestration, exercised against an in-memory `IBoardRepository`. |
| `GameOfLife.API.Tests` | End-to-end HTTP tests via `WebApplicationFactory<Program>` with the SQLite repository swapped for an in-memory one. |

All projects target `net8.0` to match the SUT.

## Commands
```bash
# Run everything
dotnet test

# Run one project
dotnet test tests/GameOfLife.Domain.Tests

# Run a single test by fully-qualified name
dotnet test --filter "FullyQualifiedName~GameApiTests.Upload_matches_documented_contract"
```

## Conventions
- **One assertion theme per test.** Test names read as sentences: `Method_does_X_when_Y`.
- **Domain tests use the `Grid(int[,])` helper** so boards read as visual literals.
- **Integration tests must stay hermetic.** The `TestApiFactory` removes the SQLite `IBoardRepository` registration and substitutes `InMemoryBoardRepository`. Never let a test touch `gameoflife.db`.
- **Validation contract is part of the public surface.** When changing `UploadBoardRequest.Validate()`, update the `Theory` cases in `Upload_rejects_invalid_grids_with_400` to match the new error message fragments.
- **Patterns to reach for** when adding rule tests: block (still life), blinker (period 2), glider (translates → won't stabilize on a small board within a tight iteration budget). Reuse them rather than inventing new shapes.

## Adding a new test
1. Pick the smallest project that owns the behavior (Domain > Application > API).
2. If it requires HTTP wiring, add it to `GameApiTests` and reuse the existing `TestApiFactory` fixture.
3. Run only that project's tests during development; run the full suite before committing.

## Things NOT to test
- Framework behavior (model binding, JSON serialization defaults).
- SQLite driver behavior — the repository is dumb persistence; cover its behavior via API integration tests, not unit tests against a real DB file.
