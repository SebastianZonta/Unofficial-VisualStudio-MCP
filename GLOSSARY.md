# VSMCP

Local MCP server driving a running Visual Studio 2026 debugger via COM automation.

## Language

**VS instance**:
A single running Visual Studio process registered in the ROT.
_Avoid_: session, process (when meaning VS itself)

**Debug session**:
An MCP-side handle binding one VS instance to subsequent tool calls.
_Avoid_: connection, instance

**Instance override**:
Optional ROT display-name prefix selecting which VS instance to bind.
_Avoid_: instance id, instance name

**Solution**:
The `.sln` open in a VS instance, owner of one or more projects.
_Avoid_: project, repo, workspace

**Project**:
A single `.csproj` loaded in a solution, unit matched by name for routing.
_Avoid_: solution, program, service

**Target process**:
A local OS process the debugger binds to through attach, such as the API under test.
_Avoid_: VS instance, debug session
