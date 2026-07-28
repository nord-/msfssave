namespace MsfsSave.Core;

/// <summary>Simulatorns tillstånd i förhållande till villkoret för dataöverföring.</summary>
public record SimReadiness(bool OnGround, bool Stationary, bool EnginesOff)
{
    /// <summary>Sant bara när planet står stilla på marken med motorerna av.</summary>
    public bool CanTransfer => OnGround && Stationary && EnginesOff;

    /// <summary>Första brist i ordningen mark → stillastående → motorer. Null när överföring tillåts.</summary>
    public string? BlockReason =>
        !OnGround ? "planet är i luften"
        : !Stationary ? "planet rör sig"
        : !EnginesOff ? "motorerna är igång"
        : null;
}
