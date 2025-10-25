namespace Nettrace.PayloadParsers;

public interface IEvent
{
    static abstract int Id { get; }
    static abstract string Name { get; }
}
