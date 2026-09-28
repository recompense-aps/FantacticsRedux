namespace Fantactics.Protocol.Connections;

/// <summary>What kind of player controls a seat.</summary>
public enum SeatControllerKind
{
    /// <summary>A person at this machine, through an <see cref="IGameConnection"/>.</summary>
    Human,

    /// <summary>A built-in bot the host plays itself.</summary>
    Bot,

    /// <summary>An LLM playing through the <c>fantactics-sim</c> CLI on a shared match file.</summary>
    Llm,
}
