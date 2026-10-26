using System.Runtime.CompilerServices;

// Server-only test seams (for example NetworkGeneratorRace.UseMatchStateSourceOnServer and
// NetworkMonster.UseMatchStateSourceOnServer / WarpOnServer).
[assembly: InternalsVisibleTo("HouseEscape.Timeline.PlayModeTests")]
