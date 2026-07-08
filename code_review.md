# Code Review: `general.improves` vs `main`

## Build / Test Validation

Validated on `net10.0` only. The Linux environment does not have the .NET Framework 4.6.2 reference assemblies, so `net462` / `netstandard2.0` targets could not be built.

| Target | Result |
|--------|--------|
| `net10.0` build | Success (2 XML-doc warnings) |
| `Trupe.Tests` | 1 failure |
| `Trupe.IntegrationTests` | Pass |
| `Trupe.Extensions.Hosting.Tests` | Pass |

---

## 1. Failing unit test: `ActorContextTest.OnTerminated_Should_SelfTell` ❌

**Location:** `tests/Trupe.Tests/ActorContextTest.cs:56`

The test expects the synchronous `Tell(...)` overload to be called, but `ActorContext.OnTerminated` invokes `TellAsync(...)`:

```csharp
// ActorContext.cs:116
Self.TellAsync(new ActorTerminated(reference, reason));
```

**Action required:** Align the implementation with the test, or update the test to expect `TellAsync`.

---

## 2. `IsAotCompatible` condition is always true ⚠️

**Location:** `src/Trupe/Trupe.csproj:10`

```xml
<PropertyGroup Condition="'$(TargetFramework)' != 'net462' OR '$(TargetFramework)' != 'netstandard2.0'">
    <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

Because the condition uses `OR`, it evaluates to `true` for every possible target framework. The likely intent was to disable AOT only on `net462` and `netstandard2.0`, which requires `AND`.

**Status:** No action required — per author, dotnet will ignore it.

---

## 3. Actor process loop silently terminates on unhandled exceptions ❌

**Location:** `src/Trupe/ActorProcess.cs:53-63`

`RunAsync` is wrapped in `Task.Run` and all exceptions are swallowed. The listener is notified, but the mailbox consumer exits permanently.

**Question:** Is this intentional? If actors should keep processing after a fault, the loop should continue instead of swallowing and exiting.

---

## 4. Supervisor `ResolveFailureAction` ignores `Stop` and `Resume` ❌

**Location:** `src/Trupe/Supervisors/AbstractSupervisor.cs:317-325`

```csharp
protected virtual FailureAction ResolveFailureAction(Child child, Exception exception)
{
    if (child.RestartCount >= MaxRestarts)
        return FailureAction.Escalate;
    return FailureAction.Restart;
}
```

`FailureAction.Stop` and `FailureAction.Resume` are never returned, despite being documented in the README.

**Action required:** Either wire `Stop`/`Resume` into the decision logic or remove them from the public API and documentation.

---

## 5. `DeadLetterActorReference` throws `NotImplementedException` ❌

**Location:** `src/Trupe/DeadLetterActorReference.cs`

Dead-letter references throw `NotImplementedException`, which is the wrong semantic. Callers cannot distinguish a missing actor from an unfinished implementation.

**Recommendation:** Throw a domain-specific exception such as `ActorNotFoundException` or `DeadLetterException`.

---

## 6. Child references may leak in the registry on supervisor restart ❌

**Location:** `src/Trupe/Supervisors/AbstractSupervisor.cs:112-138`

`BeforeRestartAsync` disposes children but does not call `registry.UnRegister(...)`. The stale references remain in `ActorProcessRegistry` until the supervisor itself is disposed.

**Recommendation:** Unregister children during pre-restart cleanup.

---

## 7. Redundant exception handling between `AskMiddleware` and `ActorProcess.ProcessAsync` ⚠️

**Locations:**
- `src/Trupe/Pipelines/Middlewares/AskMiddleware.cs`
- `src/Trupe/ActorProcess.cs:202-210`

Both layers attempt to set exceptions on `IAskMessage`. `TrySetException` prevents a crash, but the layering is confusing and can mask the actual error-handling path.

**Recommendation:** Decide which layer owns the ask completion and remove the duplicate logic.

---

## 8. `ActorReferenceProxyProcess.AskAsync<TResponse>` returns `default!` for null responses ❌

**Location:** `src/Trupe/ActorReferenceProxyProcess.cs:90-97`

```csharp
var response = await actorMessage.AsTask();
if (response != null)
    return (TResponse)response;
return default!;
```

For value-type `TResponse`, a null actor response becomes `0` / `default`. This is a silent behavioral change.

**Recommendation:** Throw `InvalidOperationException` when the response is null for a value type, or document the behavior explicitly.

---

## 9. `ActorReference.TellAsync` lacks disposal guard ❌

**Location:** `src/Trupe/ActorReference.cs:130`, `136`

The `TellAsync` overloads do not call `ObjectDisposedGuard.ThrowIf(...)`, unlike every other public method on the class.

**Recommendation:** Add the guard for consistency and to prevent use-after-dispose.

---

## 10. `ActorProcessListenerCollection.Contains` is not thread-safe ❌

**Location:** `src/Trupe/Collections/ActorProcessListenerCollection.cs:54-57`

```csharp
public bool Contains(IActorProcessListener item)
{
    return _listeners.Contains(item);
}
```

All other public accessors lock on `_locker`; this one reads the list without locking.

**Recommendation:** Add `lock (_locker)` around the read.

---

## 11. Naming inconsistencies ⚠️

| Location | Issue |
|----------|-------|
| `src/Trupe.Abstractions/IActorProcess.cs:43`, `49` | Parameters named `listing` instead of `listener` |
| `src/Trupe/ActorReference.cs:46` | Constructor `ActorReference(Uri name)` receives a URI but parameter is named `name` |
| `src/Trupe/Collections/ActorProcessListenerCollection.cs:155` | Class named `UnRegisterListiner` (typo) |

**Recommendation:** Rename for consistency and readability.

---

## 12. XML documentation warnings ⚠️

**Locations:**
- `src/Trupe.Abstractions/IActorContext.cs:37`
- `src/Trupe.Abstractions/IActorReference.cs:86`

Both reference `Ask{TResponse}(object, TimeSpan?)`, which no longer exists in the public API.

**Recommendation:** Update the `cref` to the current overload that accepts metadata.

---

## Positive Notes

- Listener pattern replacing event args is a clean simplification.
- `AsyncLocal<T>` for ambient pipeline context access is the right choice for async safety.
- Receive/send pipeline split is well aligned with the framework's goals.
- Consolidating supervisor logic into `AbstractSupervisor` removes significant duplication.
- AOT annotations and `UnconditionalSuppressMessage` justifications are thorough.

---

## Summary of Required Actions

| # | Finding | Severity |
|---|---------|----------|
| 1 | Fix `OnTerminated_Should_SelfTell` test / implementation | Blocking |
| 2 | `IsAotCompatible` condition always true | No action (dotnet ignores) |
| 3 | Decide process-loop behavior on unhandled exceptions | Blocking |
| 4 | Wire or remove `Stop`/`Resume` failure actions | Blocking |
| 5 | Replace `NotImplementedException` in dead-letter reference | High |
| 6 | Unregister children on supervisor restart | High |
| 7 | Clarify ask exception-handling ownership | Medium |
| 8 | Handle null response for value-type `AskAsync<T>` | High |
| 9 | Add disposal guard to `TellAsync` | Medium |
| 10 | Lock `Contains` in listener collection | Medium |
| 11 | Fix naming inconsistencies | Low |
| 12 | Fix XML-doc `cref` warnings | Low |
