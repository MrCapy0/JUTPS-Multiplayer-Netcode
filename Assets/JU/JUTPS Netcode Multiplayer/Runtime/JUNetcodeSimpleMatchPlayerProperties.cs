namespace JU.TPS.Netcode
{
    /// <summary>
    /// Player properties for <see cref="JUNetcodeSimpleMatchManager"/>.
    /// </summary>
    public class JUNetcodeSimpleMatchPlayerProperties : JUPlayerPropertiesBase<
        // Manager that owns these players.
        JUNetcodeSimpleMatchManager,
        // This class.
        JUNetcodeSimpleMatchPlayerProperties
    >
    {
    }
}