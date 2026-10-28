# ADR-0013: Flow runs borrow the developer's open VS instance

Status: accepted

## Context

The E2E pass starts its own VS instances and never touches any other, so every
case runs against a known, isolated IDE. Flow runs serve a different reader: a
developer checking their own app, often right after a change, with VS already
open on the solution. A dedicated instance per flow run would cost one to three
minutes of startup and a second IDE on the developer's machine every time, and
its solution-load state could differ from the one the developer actually uses.

## Decision

A flow run binds the developer's already-open VS instance(s), found by solution
name and always attached with `instanceOverride`. It never starts or closes a
VS instance. It leaves each instance as it found it:

- detach on every exit path, which deletes the breakpoints the debug session created;
- reset every exception setting the run changed, because detach does not revert
  them (ADR-0011);
- never touch foreign breakpoints. A stop on one is an observation and the run resumes.

An instance that is already debugging a target process (not in `design`) makes
the run BLOCKED rather than taking it over. A flow that reaches a second target
process needs a second open VS instance, because one debug session binds one VS
instance (ADR-0008) and debugs one target process. A missing one blocks only the
checkpoints in that process.

Alternative considered: a fresh VS instance per flow run, as the E2E pass does.
Rejected because the startup cost and the extra IDE fall on every run, while the
isolation it buys matters for verifying the server, not the developer's app.

## Consequences

Flow runs start in seconds and need no VS setup beyond what the developer already
has open. Accepted risks:

- the developer's own breakpoints and persisted solution state can add stops,
  which show up as observations rather than failures;
- a flow run cannot run while the developer is debugging in that instance;
- an agent killed mid-run can leave a changed exception setting behind, until
  the next flow run or a manual `default` reset.
