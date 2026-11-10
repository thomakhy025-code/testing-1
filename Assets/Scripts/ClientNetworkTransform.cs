using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Owner-authoritative transform sync. Each player moves their own character,
/// and Netcode replicates that motion to the other player.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
