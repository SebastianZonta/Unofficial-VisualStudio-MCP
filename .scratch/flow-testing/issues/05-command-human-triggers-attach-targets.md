# 05: command and human triggers, attach targets

**What to build:** Flow plans may use a `command` trigger (exact shell command the flow runner executes, e.g. an HTTP request) or a `human` trigger (instruction the developer performs), and target an already-running process by its recorded process name. The planner writes them; the flow runner binds `attach` targets via `list_processes` + `attach_process` with `consent: true`, executes `command` triggers verbatim, and for `human` returns to the main agent, which asks the developer to act and resumes the flow runner. The flow runner never invents a trigger. See [spec](../spec.md).

**Blocked by:** 04 (run-flow: one flow, one solution, launch)

**Status:** done

- [x] Planner produces `command` and `human` triggers when the entry point calls for them
- [x] `attach` target found by recorded process name; not found → BLOCKED with the fix
- [x] `command` trigger runs exactly as written in the plan
- [x] `human` trigger: flow runner pauses, main agent asks the developer, resumed flow runner continues to a verdict
- [x] Plan without a trigger → BLOCKED, never an invented one
- [x] Dogfood on the Sandbox copy covers one `human`-triggered flow end to end
