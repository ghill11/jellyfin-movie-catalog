# C# style rules for jellyfin-movie-catalog

Language-specific identifier, structure, and idiom conventions for the C# half of the project (the Jellyfin plugin). The language-agnostic conventions (em-dash prohibition, comment philosophy, error-handling principles, logging principles, forbidden practices) live in `style.md`; this file extends them with the C#-specific layer.

## Identifier conventions

- `PascalCase` for: types (classes, structs, interfaces, enums), methods, properties, events, public fields, namespaces.
- `camelCase` for: method parameters, local variables.
- `_camelCase` (leading underscore) for: private and protected instance fields. The underscore makes field-vs-local visually unambiguous at the call site.
- `PascalCase` for: constants and `static readonly` members. This differs from Python (`UPPER_SNAKE_CASE`) and Java (`UPPER_SNAKE_CASE`); the C# convention is to use PascalCase for all member-level names regardless of mutability. Follow the language convention here even though it crosses idioms from other languages.
- Interface names: `IPascalCase` (the leading `I` is canonical C# convention, not a sigil; preserve it).
- Type parameters: `TPascalCase` (the leading `T` is canonical; `TConfiguration`, `TResult`, etc.).

## File and project structure

- One public type per file. File name matches the type name: `MovieCatalogBuilder.cs` contains `class MovieCatalogBuilder`. Nested private types are fine in the same file.
- Project file (`.csproj`) name matches the assembly name and the top-level namespace.
- Folder structure mirrors namespace structure where reasonable; do not over-fragment.

## Nullable reference types

`<Nullable>enable</Nullable>` in every `.csproj` is mandatory. Treat the nullable-reference-types annotations as load-bearing:

- A reference type without `?` is non-nullable. The compiler enforces it.
- A reference type with `?` is nullable. Callers must null-check.
- `null!` (the null-forgiving operator) is forbidden in production code unless accompanied by an inline comment explaining why the compiler is wrong about this site. Acceptable cases are rare (test helpers, certain framework-injection patterns); each occurrence is reviewed.
- Do NOT silence nullable warnings globally with `#nullable disable`. If a third-party API returns `T` but is actually `T?`, wrap it at the boundary and convert to `T?` at the wrapper's edge.

## Async and await

- Methods that return `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>` MUST have the `Async` suffix in their name: `BuildCatalogAsync`, `PushSnapshotAsync`. The suffix is a contract: callers know to `await` it without reading the signature.
- Never `.Result`, never `.Wait()`, never `.GetAwaiter().GetResult()`. These deadlock under synchronization contexts and silently degrade throughput under thread-pool exhaustion. If a caller cannot be async (an event handler signature that returns `void`), fire-and-forget via a separate dispatched task and surface exceptions explicitly.
- `await ... .ConfigureAwait(false)` is the default in library code (code that does not own its synchronization context). The Jellyfin plugin's hosted services are library code: prefer `ConfigureAwait(false)` on every internal `await`. Top-level UI / app code can omit it.
- Async event handlers (responding to Jellyfin library events) MUST return immediately. The handler signature stays `void` or returns `Task` without awaiting the actual work. The actual work goes to a debounced background dispatcher (see `architecture.md` §"Inviolable structural rules"). Blocking the event-publishing thread inside a handler is a `code-reviewer` BLOCK.

## Disposable and resource management

- Prefer `using` declarations (statement form, C# 8+) over `using` blocks when scope allows. Less indentation, same semantics.
  ```csharp
  using var stream = File.OpenRead(path);
  // stream disposed at end of enclosing scope
  ```
- `IAsyncDisposable` types use `await using`, never plain `using`. Mixing them silently skips the async disposal.
- Long-lived objects (`HttpClient`, the GitHub API client) are injected via DI as singletons, not constructed per-call. Constructing a new `HttpClient` per request leaks sockets.

## Dependency injection

- The plugin registers services via `IPluginServiceRegistrator`. Register concrete types against their interface (or against themselves when no interface exists).
- Constructor injection only. Do not use property injection or service-locator patterns inside types.
- Singletons for stateless services and shared clients (`HttpClient`, the catalog builder, the debouncer). Transients for per-call work.

## Logging

- `ILoggerFactory.CreateLogger(string categoryName)` is the canonical way to obtain a logger inside this plugin. Inject `ILoggerFactory`, then call `CreateLogger(nameof(MyClass))` in the constructor. Cache the resulting `ILogger` in a `_logger` field.
- Do NOT inject `ILogger<T>` directly. The generic-typed logger has a known interaction issue in some Jellyfin DI configurations where the resolution fails or returns a no-op logger. Use the factory pattern instead. (This is a Jellyfin-specific workaround, not a general C# recommendation.)
- Log levels: `Debug` for development trace, `Information` for normal lifecycle events, `Warning` for recoverable anomalies, `Error` for failures that block work, `Critical` reserved for "service is broken." Use structured logging (named parameters), not string interpolation:
  ```csharp
  _logger.LogInformation("Pushed snapshot with {Count} movies to {Repo}", count, repo);
  ```
- Never log secrets. The GitHub PAT is the only secret in this project today; it must not appear in any log line at any level. Build a check at the point where the PAT is read from config: log the fact-of-presence and the length, never the value.
- Never log a movie's full Jellyfin metadata blob (paths, file sizes, etc. are personal-use but still avoid the noise). Log counts and durations.

## NuGet package hygiene

- Pin every package version exactly. No floating ranges (`1.2.*`), no `latest`. Reproducible builds depend on the lock being deterministic.
- Prefer `Microsoft.Extensions.*` abstractions (logging, DI, configuration) over third-party alternatives where Jellyfin already provides DI for them.
- Adding a new NuGet dependency is a design decision: state it in the plan and justify why an existing dep or a stdlib pattern does not cover the need. Each new dep is a transitive-dependency exposure.

## Exception handling

- Catch `Exception` only at the boundary (top of a hosted service's main loop, the dispatch entrypoint for a debounced work item). Internal code catches specific exceptions or lets them propagate.
- Never `catch (Exception) { /* nothing */ }`. A bare swallow is a `code-reviewer` BLOCK. Either log and re-throw, log and recover, or do not catch.
- `ArgumentNullException.ThrowIfNull(arg)` is the modern guard form for non-nullable parameters that come from external sources (JSON deserialization, framework callbacks).

## Comment philosophy (C#-specific extension)

The language-agnostic philosophy ("comments explain WHY, not WHAT") in `style.md` applies here too. C#-specific notes:

- XML doc comments (`/// <summary>...</summary>`) on PUBLIC types and members only. Private members get plain `//` comments if any. Do not generate XML docs for everything; the noise hides the meaningful ones.
- `// TODO:` comments require a tracking link (issue number, plan file path). A bare `// TODO:` without context is a `_todo_<verb>_` forbidden-name equivalent (per `style.md` §"Forbidden practices").

## Forbidden in C# code

- `dynamic` outside of explicit interop scenarios. The runtime resolution defeats the type system.
- `unsafe` blocks are not used in this plugin. Adding one is a design decision the user approves explicitly before merge.
- Reflection-based property access (`type.GetProperty(name).GetValue(obj)`) outside of a JSON-deserialization boundary. Use typed access.
- `Thread.Sleep` in production code. `await Task.Delay(...)` is the async equivalent and does not block a thread.
- `Task.Run` to "convert" sync code to async. If a method is synchronous and CPU-bound, leave it sync and let callers decide; do not hide blocking work inside an async-looking signature.
- `goto` in any form.
