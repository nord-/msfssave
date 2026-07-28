namespace MsfsSave.Core;

/// <summary>Kastas när dataöverföring försöks medan planet inte står stilla på marken med motorerna av.</summary>
public class SimNotReadyException : Exception
{
    public SimNotReadyException(string reason) : base(reason) => Reason = reason;

    /// <summary>Orsaken i klartext, avsedd för statusraden.</summary>
    public string Reason { get; }
}
