namespace Nettrace.PayloadParsers;

/*
 TaskScheduled 70270 MetadataPayload (6 fields):
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
   FieldV1 { TypeCode = 9, FieldName = TaskID }
   FieldV1 { TypeCode = 9, FieldName = CreatingTaskID }
   FieldV1 { TypeCode = 9, FieldName = TaskCreationOptions }
   FieldV1 { TypeCode = 9, FieldName = appDomain }
*/
public sealed record TaskScheduled(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId, int CreatingTaskId, int TaskCreationOptions, int AppDomain) : IEvent
{
    public static string Name => nameof(TaskScheduled);
}

/*
 TaskWaitBegin 1 MetadataPayload (5 fields):
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
    FieldV1 { TypeCode = 9, FieldName = TaskID }
    FieldV1 { TypeCode = 9, FieldName = Behavior }
    FieldV1 { TypeCode = 9, FieldName = ContinueWithTaskID }
*/
public sealed record TaskWaitBegin(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId, int Behavior, int ContinueWithTaskId) : IEvent
{
    public static string Name => nameof(TaskWaitBegin);
}

/*
 AwaitTaskContinuationScheduled 2 MetadataPayload (3 fields):
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
    FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
    FieldV1 { TypeCode = 9, FieldName = ContinueWithTaskId }
*/
public sealed record AwaitTaskContinuationScheduled(int OriginatingTaskSchedulerId, int OriginatingTaskId, int ContinueWithTaskId) : IEvent
{
    public static string Name => nameof(AwaitTaskContinuationScheduled);
}

/*
 TraceOperationRelation 6 (2 fields):
    FieldV1 { TypeCode = 9, FieldName = TaskID }
    FieldV1 { TypeCode = 9, FieldName = Relation }
*/
public sealed record TraceOperationRelation(int TaskId, int Relation) : IEvent
{
    public static string Name => nameof(TraceOperationRelation);
}

/*
 NewID 1 (1 fields):
   FieldV1 { TypeCode = 9, FieldName = TaskID }
*/
public sealed record NewId(int TaskId) : IEvent
{
    public static string Name => "NewID";
}

/*
 TraceSynchronousWorkBegin 2 (2 fields):
   FieldV1 { TypeCode = 9, FieldName = TaskID }
   FieldV1 { TypeCode = 9, FieldName = Work }
*/
public sealed record TraceSynchronousWorkBegin(int TaskId, int Work) : IEvent
{
    public static string Name => nameof(TraceSynchronousWorkBegin);
}

/*
 TraceSynchronousWorkEnd 6 (1 fields):
   FieldV1 { TypeCode = 9, FieldName = Work }
*/
public sealed record TraceSynchronousWorkEnd(int Work) : IEvent
{
    public static string Name => nameof(TraceSynchronousWorkEnd);
}

/*
 TraceOperationEnd 8 (2 fields):
   FieldV1 { TypeCode = 9, FieldName = TaskID }
   FieldV1 { TypeCode = 9, FieldName = Status }
*/
public sealed record TraceOperationEnd(int TaskId, int Status) : IEvent
{
    public static string Name => nameof(TraceOperationEnd);
}

/*
 TaskWaitContinuationStarted 1 (1 fields):
   FieldV1 { TypeCode = 9, FieldName = TaskID }
*/
public sealed record TaskWaitContinuationStarted(int TaskId) : IEvent
{
    public static string Name => nameof(TaskWaitContinuationStarted);
}

/*
 TraceOperationBegin 4 (3 fields):
   FieldV1 { TypeCode = 9, FieldName = TaskID }
   FieldV1 { TypeCode = 18, FieldName = OperationName }
   FieldV1 { TypeCode = 11, FieldName = RelatedContext }
*/
public sealed record TraceOperationBegin(int TaskId, string OperationName, long RelatedContext) : IEvent
{
    public static string Name => nameof(TraceOperationBegin);
}

/*
 TaskWaitContinuationComplete 7 (1 fields):
   FieldV1 { TypeCode = 9, FieldName = TaskID }
*/
public sealed record TaskWaitContinuationComplete(int TaskId) : IEvent
{
    public static string Name => nameof(TaskWaitContinuationComplete);
}

/*
 TaskWaitEnd 9 (3 fields):
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskSchedulerID }
   FieldV1 { TypeCode = 9, FieldName = OriginatingTaskID }
   FieldV1 { TypeCode = 9, FieldName = TaskID }
*/
public sealed record TaskWaitEnd(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId) : IEvent
{
    public static string Name => nameof(TaskWaitEnd);
}

/*
  ProcessInfo 1 MetadataPayload (3 fields):
    FieldV1 { TypeCode = 18, FieldName = CommandLine }
    FieldV1 { TypeCode = 18, FieldName = OSInformation }
    FieldV1 { TypeCode = 18, FieldName = ArchInformation }
*/
public sealed record ProcessInfo(string CommandLine, string OsInformation, string ArchInformation) : IEvent
{
    public static string Name => nameof(ProcessInfo);
}
