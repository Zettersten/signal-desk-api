# Agent instructions

## Git workflow
- Commit directly to `main`. Do not create branches or pull requests unless the user explicitly asks.
- Commit messages end with `Co-Authored-By: Warp <agent@warp.dev>`.
- Do not commit `.agents/`, `.claude/`, or `skills-lock.json` unless the user asks.

## API conventions (FastEndpoints)
Follow the project skill at `.agents/skills/fast-endpoints/SKILL.md` for endpoint work.
- Input validation lives in a `Validator<TRequest>` class (FluentValidation), not in handlers.
- Use `Send.*` methods for responses: `Send.NoContentAsync`, `Send.NotFoundAsync`, `Send.ResultAsync(TypedResults.Conflict())`.
- Use `ThrowError("message")` for domain 400 errors inside `HandleAsync`.
- Use `ApiException` only for statuses without a FastEndpoints equivalent (for example 429 and 503).
- Keep domain checks such as catalog membership and state conflicts in the handler.

## Code rules
- One top-level type per file. MA0048 is enforced and must not be suppressed in `.editorconfig`.
- Analyzer warnings are errors (`TreatWarningsAsErrors`). Fix them rather than suppressing them.
- Stop any running API process before building, because it locks `SignalDesk.Api.exe`.

## Build and test
- `dotnet build SignalDesk.slnx`
- `dotnet test SignalDesk.slnx`
- Restore with `dotnet restore SignalDesk.slnx --locked-mode` if `obj/` is removed.
