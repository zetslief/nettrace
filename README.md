# NTR

Exploring `nettrace`.

There are several opened questions regarding `nettrace`:
* What is `nettrace`? How fast it could be deserialized?
* How far we can go with just debug port?

Useful links:
* [Nettrace](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md)
* [EventListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventlistener?view=net-9.0)
* [IPC protocol](https://github.com/dotnet/diagnostics/blob/main/documentation/design-docs/ipc-protocol.md)
* [CLR events: go for the nettrace file format!](https://chnasarre.medium.com/clr-events-go-for-the-nettrace-file-format-6b363364c2a3)

TODO:
1. Parse content of TPL provider.
2. Render this content. So it is clear which events happened when.
