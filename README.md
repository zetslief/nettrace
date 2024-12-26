# NTR

Exploring `nettrace`.

There are several opened questions regarding `nettrace` and debugging in `.NET`:
* What is `nettrace`? How fast it could be deserialized?
* How far we can go with just debug port?
* How does their IPC work?

Currently, `NTR` implements just deserialization. I want to write an explorer: a program that would allow visualizing the traces.

Useful links:
* [Nettrace](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md)
* [EventListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventlistener?view=net-9.0)
* [IPC protocol](https://github.com/dotnet/diagnostics/blob/main/documentation/design-docs/ipc-protocol.md)
* [CLR events: go for the nettrace file format!](https://chnasarre.medium.com/clr-events-go-for-the-nettrace-file-format-6b363364c2a3)