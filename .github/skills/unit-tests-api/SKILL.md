---
name: unit-tests-api
description: "Write minimal xUnit tests for .NET/C# changes in the unified-scheduling API. Use when adding tests for endpoints, validators, services, or handlers in api/. Covers test project layout, naming conventions, fixture setup, and run commands."
---

# API Unit Tests — .NET / xUnit

## Scope
Applies to files under `api/` and tests under `api/Unified.Tests/`.

## Project Layout
```
api/
  Unified.Tests/
    Unified.Core/
      Controllers/   ← controller-level tests
      Services/      ← service/domain tests
    Unified.Infrastructure/
    Unified.UserManagement/
    Unified.Tests.csproj
    xunit.runner.json
```

## Procedure

1. **Identify changed behavior** — read the modified production file(s) and note public method signatures, return types, and exception paths.
2. **Find the nearest test file** — look inside `api/Unified.Tests/` for an existing file that tests the same class or a sibling. Mirror its fixture style, helpers, and `using` statements exactly.
3. **Write minimal tests** — cover:
   - Happy path (valid input → expected output/state)
   - Key validation / guard-clause paths
   - Any new error/exception branches
4. **Run tests** — use the scoped command below; do **not** run the full solution unless asked.
5. **Report** results using the Output Format.

## Naming Convention
```
MethodName_StateUnderTest_ExpectedBehavior
// e.g. GetShift_WhenShiftNotFound_ReturnsNotFound
```

## Run Commands
```bash
# Run only the Unified.Tests project
dotnet test api/Unified.Tests/Unified.Tests.csproj

# Filter to a single class (xUnit v3 style)
dotnet test api/Unified.Tests/Unified.Tests.csproj -- --filter-class <FullyQualifiedClassName>
```

## Conventions
- Use `xUnit` (`[Fact]`, `[Theory]`) — do **not** introduce NUnit or MSTest.
- Prefer real collaborators (in-memory EF, value objects) over mocks; use `Moq` only when an external boundary (HTTP client, clock) must be isolated.
- Do **not** make real network calls or depend on wall-clock time.
- Match existing `Arrange / Act / Assert` comment style if present in nearby tests.
- Keep each test class in the same sub-folder structure as the production class it tests.
