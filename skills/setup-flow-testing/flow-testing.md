# Flow testing

How a flow runner reaches this repo's app under the Visual Studio debugger. Written by `setup-flow-testing`; read by `plan-flow` and `run-flow`. Edit freely; rerun `setup-flow-testing` to regenerate.

## Elevation

VS and the MCP host must run at the same elevation. A non-elevated MCP host cannot see an elevated VS instance in the ROT, so `list_instances` comes back empty. Start VS at the same elevation as the MCP host, with the solution open.

## Solutions

| Solution | Path |
|---|---|
| `<App>` | `<path/to/App.sln>` |

Each solution must be open in its own VS instance before a run. Bind the VS instance whose `list_instances` entry shows this solution open, by that entry's display name; the instance override carries the VS PID, so it changes with every VS start and is never recorded here.

## Build

```
<build command, e.g. dotnet build path/to/App.sln>
```

## Targets

| Target | Solution | How | Details |
|---|---|---|---|
| `<orders-api>` | `<App>` | `launch` | exe `<path/to/bin/Debug/net10.0/Orders.Api.exe>`, args `<--urls http://localhost:5000>`, working dir `<path/to/Orders.Api>` |
| `<worker>` | `<App>` | `attach` | process name `<Orders.Worker>` |

Paths are relative to the repo root. Arguments are defaults; a flow plan's `launch` trigger may name its own.

## Flows

- Flow index: `<docs/flows/INDEX.md>`
- Flow plans: `<docs/flows>/<area>/<flow>.md` (committed)
- Run reports: `<docs/flows/runs/>` (gitignored)
