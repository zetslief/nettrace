# NTR

Exploring `nettrace`.

Useful links:
* [Nettrace V6](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/NetTraceFormat.md)
* [Nettrace V5](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/NetTraceFormat_v5.md)
* [EventListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventlistener?view=net-9.0)
* [IPC protocol](https://github.com/dotnet/diagnostics/blob/main/documentation/design-docs/ipc-protocol.md)
* [CLR events: go for the nettrace file format!](https://chnasarre.medium.com/clr-events-go-for-the-nettrace-file-format-6b363364c2a3)
* [.NET Events Viewer](https://github.com/verdie-g/dotnet-events-viewer.git)
* [Profiling API](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/profiling.md)

TODO:
- Explorer
    - [ ] Add tabs for every kind of blocks in the files.
    - [ ] For events, provide a way to explore them per provider.
- profileme:
    - [ ] Add mode that allows synchronized start - to capture full trace.
    - [ ] Ensure it triggers all TPL events.
- Data parsing logic should be shared between the explrer and the nettrace lib.
    - [ ] `NettraceReader.ReadUnicode` should be moved out.
    - [ ] `MoveBy` should be moved out and mb even repalced with ranges.
- Move types outside of the `NettraceReader`. It is annoying to prefix them with `NettraceReader`.
- Paylod Parsers
    - Verify input.
    - Ensure whole payload is read.

## Runtime Rundown events

> Runtime rundown events have empty names. See [Issue](https://github.com/dotnet/runtime/issues/96365).

This events are described only in `ClrEtwAll.man`.

These events often have multiple versions, which share the same IDs.
Related metadata header contains version number.
