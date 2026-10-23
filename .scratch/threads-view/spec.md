# Threads View

Status: ready-for-agent

## Problem Statement

Multithreaded target processes (Kestrel servers serve each request on pool threads) defeat single-thread inspection: `read_stack` and `read_variables` show one anonymous call stack, so step-through on one request silently interleaves with work on other threads and the agent cannot tell which thread hit the breakpoint. Agents debugging web servers and background workers need to see all threads of the debugged target process, pin execution by freezing noisy threads, and address stack/variable reads to a chosen thread.

## Solution

Session-scoped thread awareness: a `list_threads` tool plus `freeze_thread` / `thaw_thread` controls, and optional thread addressing on `read_stack` / `read_variables` (default = the thread that hit the breakpoint). All thread operations require break mode and are scoped to the target process of that debug session only — threads never leak across sessions.

## User Stories

1. As an agent, I want to list all threads of the debugged target process in the current debug session, so that I can see concurrent request and worker threads instead of one anonymous stack.
2. As an agent, I want each thread entry to carry its thread id, name, current function, and frozen state, so that I can identify the thread I care about at a glance.
3. As an agent, I want thread listing scoped to the target process of my debug session, so that sibling sessions debugging other processes never contaminate my view.
4. As an agent, I want thread listing to require break mode, so that I never read a moving thread set mid-run.
5. As an agent, I want to freeze a noisy background thread, so that stepping my request thread is not interrupted by unrelated breakpoints and work.
6. As an agent, I want to thaw a previously frozen thread, so that I can restore full concurrency after isolating the bug.
7. As an agent, I want freezing one thread to leave all other threads and sessions unaffected, so that isolation is surgical.
8. As an agent, I want `read_stack` addressable per thread with the default being the thread that hit the breakpoint, so that existing single-thread workflows keep working unchanged.
9. As an agent, I want `read_variables` addressable per thread with the default being the thread that hit the breakpoint, so that locals I read belong to the request I am debugging.
10. As an agent, I want per-thread reads to require break mode like their unthreaded equivalents, so that I never inspect a running frame.
11. As an agent, I want a clear error when I address a thread id that does not exist in my session, so that stale ids from a previous break fail loudly instead of returning the wrong stack.
12. As an agent, I want frozen state visible on every thread entry, so that I can tell which threads I pinned before resuming.
13. As an agent, I want thread ids from one debug session rejected in another session, so that a recycled id can never address the wrong target process.
14. As a builder, I want all thread operations behind the single bridge seam, so that tools stay thin and EnvDTE thread access stays in one place.

## Implementation Decisions

- New bridge-seam operations: list threads for a debug session, freeze a thread, thaw a thread; plus an optional thread selector on the existing stack-read and variable-read seam contracts. The `IDebugBridge` seam is the only boundary crossed; tools stay thin and delegate through it, with `FakeDebugBridge` as the in-memory test double.
- Session scoping rule: every thread operation resolves threads through the target process bound to the given debug session; thread ids are opaque per-session keys and are never valid across sessions. Unknown or foreign ids fail with the standard session/thread error, never with another session's data.
- Default-thread rule: when no thread is specified, `read_stack` / `read_variables` address the thread that hit the breakpoint (the current thread), preserving current single-thread behavior exactly.
- Break-mode gate: list, freeze, thaw, and per-thread reads all require break mode and fail with the existing mode error otherwise, mirroring the inspection-tool contract.
- Thread entry contract: numeric thread id usable directly as the read/freeze selector, thread name (empty when unnamed), current function name, frozen flag. Ordering is deterministic by thread id ascending.
- Freeze semantics: freezing suspends scheduling of that thread only while the debugger is resumed; it does not detach, terminate, or affect other threads, sessions, or the VS instance. Thaw reverses it; thawing an unfrozen thread succeeds as a no-op with the thread reported unfrozen.
- Glossary vocabulary governs naming: debug session versus target process versus VS instance; mode and lastHit keep their existing meanings; session-less does not apply — every thread operation takes a debug session.
- Live COM mapping snapshots EnvDTE thread state to plain DTOs; live handles never escape; COM errors map through the existing error mapper.

## Testing Decisions

- Good tests assert external behavior through public seams only: tool output envelopes, bridge seam results, default-thread behavior, break-mode gating, and session isolation. No assertions on EnvDTE internals, call order, or private state.
- Tested surface: `list_threads` at the tool-call boundary against `FakeDebugBridge` (entries, frozen flags, deterministic order); freeze/thaw round-trip (frozen flag flips, idempotent thaw); per-thread `read_stack` / `read_variables` returning the addressed thread's frames and defaulting to the breakpoint-hit thread when unspecified; break-mode rejection for all four operations; cross-session thread-id rejection.
- Prior art: tool-boundary tests against the fake bridge in the style of the existing inspection tests; fake-seeded thread fixtures without touching a real debugger, run with `dotnet test VsDebugMcp.slnx` (no VS needed).
- Live coverage stays manual: attach to a multithreaded target, break, list threads, freeze all but one, step, verify per-thread stacks differ and the default thread is the hitting thread.

## Out of Scope

- Stepping or resuming a single thread while others stay frozen beyond what freeze/thaw already enables (no per-thread continue primitive).
- Naming or renaming OS threads; thread names are read-only as reported by the debugger.
- Cross-process or cross-session thread views; one call shows exactly one session's target process.
- Deadlock detection, thread-cell ownership analysis, or any interpretation of what threads are doing.
- Persisting frozen state across detach or MCP restarts; freeze is a live-debug aid only.
- Any change to `attach_process` semantics, the mine/consent gates, or VS PID versus target PID handling.

## Further Notes

- Thread ids are recycled by the runtime: the agent should list threads fresh after each break rather than caching ids across resumes.
- Freeze is best-effort scheduling suppression, not a correctness fence; threadpool threads doing I/O may still complete callbacks the agent should expect.
