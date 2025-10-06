# NTR

Exploring `nettrace`.

Useful links:
* [Nettrace V6](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/NetTraceFormat.md)
* [Nettrace V5](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/NetTraceFormat_v5.md)
* [EventListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventlistener?view=net-9.0)
* [IPC protocol](https://github.com/dotnet/diagnostics/blob/main/documentation/design-docs/ipc-protocol.md)
* [CLR events: go for the nettrace file format!](https://chnasarre.medium.com/clr-events-go-for-the-nettrace-file-format-6b363364c2a3)

TODO:
- TPL
    - [x] Parse basic events.
    - [ ] Move parsing logic into a separate project.
- Explorer
    - [ ] Create simple list of all events.
    - [ ] Group events by thread id.
    - [ ] Gropu events by task id.
