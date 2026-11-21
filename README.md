# Tralys.Analyzers

Roslyn analyzers for rules that hold in any C# codebase.

```xml
<PackageReference Include="Tralys.Analyzers" Version="0.1.0" />
```

It is a development dependency, so it never reaches the consumers of a package that uses it.

## Rules

| Rule | Severity | Reports |
|---|---|---|
| TRA0001 | Error | A null-forgiving operator: `x!`, `null!`, `default!` |
| TRA0002 | Error | A `[SuppressMessage]` naming TRA0001, TRA0003, TRA0004 or TRA0005 |
| TRA0003 | Error | `#nullable disable`, a `#pragma` or `.editorconfig` severity that silences a nullable warning |
| TRA0004 | Error | `Nullable` not `enable`, warning level 0, `NoWarn`/`WarningsNotAsErrors` for a nullable warning |
| TRA0005 | Error | `[AllowNull]` or `[NotNull]` on a field or auto-property |

## The compiler's null checks stay on

`!` tells the compiler a value is not null without making it so. When that is wrong, the null surfaces as a
`NullReferenceException` somewhere else, and the compiler can no longer point at it. Switching the checks off for a
file or a project, or letting a field store a null its reads deny, hides the same thing more widely. Make the code say
what is true instead:

- a non-nullable type, or a `required` member;
- `[NotNullWhen]`, `[MemberNotNull]` and the other nullable attributes on the API that knows;
- a check where the null can occur: `value ?? throw new InvalidOperationException("why it cannot be null")`.

None of `#pragma warning disable`, `<NoWarn>`, an `.editorconfig` severity or `[SuppressMessage]` silences these
rules, and there is no allowance for existing code: a codebase fixes it before it takes the package.

TRA0005 allows `[AllowNull]` alongside `[MaybeNull]`, which is an honest nullable, and on storage typed by an open
type parameter, such as a pooled slot cleared to `default`, which C# has no other way to spell.

Generated code is not checked.
