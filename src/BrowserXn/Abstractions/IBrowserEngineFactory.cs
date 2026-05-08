namespace GDD.Abstractions;

public interface IBrowserEngineFactory
{
    IBrowserEngine Create(int playerId);
}
