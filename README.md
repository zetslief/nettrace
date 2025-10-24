# NTR

Exploring `nettrace`.

Useful links:
* [Nettrace V6](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/NetTraceFormat.md)
* [Nettrace V5](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/NetTraceFormat_v5.md)
* [EventListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventlistener?view=net-9.0)
* [IPC protocol](https://github.com/dotnet/diagnostics/blob/main/documentation/design-docs/ipc-protocol.md)
* [CLR events: go for the nettrace file format!](https://chnasarre.medium.com/clr-events-go-for-the-nettrace-file-format-6b363364c2a3)
* [.NET Events Viewer](https://github.com/verdie-g/dotnet-events-viewer.git)

TODO:
- TPL
    - [x] Parse basic events.
    - [x] Move parsing logic into a separate project.
    - [ ] Build tests to trigger specific TPL events.
    - [ ] `TaskWaitBegin.ContinueWithTaskId` might contain incorrect value.
- Explorer
    - [ ] Create simple list of all events.
    - [ ] Group events by thread id.
    - [ ] Gropu events by task id.
- profileme:
    - [ ] Add mode that allows synchronized start - to capture full trace.
- Data parsing logic should be shared between the explrer and the nettrace lib.
    - [ ] `NettraceReader.ReadUnicode` should be moved out.

## Stacks

[StackBlock](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/NetTraceFormat_v5.md#stackblock-object)
> On 32 bit traces the stack bytes can be interpreted as an array of 4 byte integer IPs. On 64 bit traces it is an array of 8 byte integer IPs. Each stack can be given an ID by starting with First Id and incrementing by 1 for each additional stack in the block in stream order.

^ this is super confusing. Explore if runtime rundown events could help somehow.
This events are described only in `ClrEtwAll.man`.
