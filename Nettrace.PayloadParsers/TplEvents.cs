namespace Nettrace.PayloadParsers;

public static class TplProvider
{
    public const string Name = "System.Threading.Tasks.TplEventSource";
}

public sealed record TaskScheduled(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId, int CreatingTaskId, int TaskCreationOptions, int AppDomain) : IEvent
{
    public static int Id => 7;
    public static string Name => nameof(TaskScheduled);
}

public sealed record TaskWaitBegin(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId, int Behavior, int ContinueWithTaskId) : IEvent
{
    public static int Id => 10;
    public static string Name => nameof(TaskWaitBegin);
}

public sealed record TaskWaitEnd(int OriginatingTaskSchedulerId, int OriginatingTaskId, int TaskId) : IEvent
{
    public static int Id => 11;
    public static string Name => nameof(TaskWaitEnd);
}

public sealed record AwaitTaskContinuationScheduled(int OriginatingTaskSchedulerId, int OriginatingTaskId, int ContinueWithTaskId) : IEvent
{
    public static int Id => 12;
    public static string Name => nameof(AwaitTaskContinuationScheduled);
}

public sealed record TaskWaitContinuationComplete(int TaskId) : IEvent
{
    public static int Id => 13;
    public static string Name => nameof(TaskWaitContinuationComplete);
}

public sealed record TraceOperationBegin(int TaskId, string OperationName, long RelatedContext) : IEvent
{
    public static int Id => 14;
    public static string Name => nameof(TraceOperationBegin);
}

public sealed record TraceOperationEnd(int TaskId, int Status) : IEvent
{
    public static int Id => 15;
    public static string Name => nameof(TraceOperationEnd);
}

public sealed record TraceOperationRelation(int TaskId, int Relation) : IEvent
{
    public static int Id => 16;
    public static string Name => nameof(TraceOperationRelation);
}

public sealed record TraceSynchronousWorkBegin(int TaskId, int Work) : IEvent
{
    public static int Id => 17;
    public static string Name => nameof(TraceSynchronousWorkBegin);
}

public sealed record TraceSynchronousWorkEnd(int Work) : IEvent
{
    public static int Id => 18;
    public static string Name => nameof(TraceSynchronousWorkEnd);
}

public sealed record TaskWaitContinuationStarted(int TaskId) : IEvent
{
    public static int Id => 19;
    public static string Name => nameof(TaskWaitContinuationStarted);
}

public sealed record NewId(int TaskId) : IEvent
{
    public static int Id => 26;
    public static string Name => "NewID";
}
