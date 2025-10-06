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
  ProcessInfo 1 MetadataPayload (3 fields):
    FieldV1 { TypeCode = 18, FieldName = CommandLine }
    FieldV1 { TypeCode = 18, FieldName = OSInformation }
    FieldV1 { TypeCode = 18, FieldName = ArchInformation }
*/
public sealed record ProcessInfo(
    string CommandLine,
    string OsInformation,
    string ArchInformation
) : IEvent
{
    public static string Name => nameof(ProcessInfo);
}
