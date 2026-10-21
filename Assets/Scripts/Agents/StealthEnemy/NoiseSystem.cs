using System;
using UnityEngine;

// =====================================================================
//  NoiseSystem  -  [IS] Stealth Enemy (Peiris)
//  A tiny global "sound bus". Anything that makes a noise calls
//  NoiseSystem.Emit(...). Anything that listens (the Stealth Enemy's
//  StealthPerception) subscribes to NoiseSystem.NoiseEmitted.
//
//  Why a bus?  The noise makers (player, rocks, traps) never need a
//  reference to the enemies, and the enemies never need a reference to
//  the noise makers. This keeps the four agents decoupled, so one
//  agent can be removed without breaking the others.
//
//  Teammates: a trap explosion can call
//      NoiseSystem.Emit(transform.position, 4f, NoiseSource.Explosion);
//  and the Stealth Enemy will come to investigate it.
// =====================================================================

public enum NoiseSource { Footstep, Gunshot, Melee, Impact, Explosion, Other }

public struct NoiseEvent
{
    public Vector3 Position;
    public float Loudness;        // 1 = a normal footstep heard from ~1 m. Gunshot is ~3.
    public NoiseSource Source;
    public float Time;

    public NoiseEvent(Vector3 position, float loudness, NoiseSource source)
    {
        Position = position;
        Loudness = loudness;
        Source = source;
        Time = UnityEngine.Time.time;
    }
}

public static class NoiseSystem
{
    public static event Action<NoiseEvent> NoiseEmitted;

    // Throwable reports (loudness * impact speed), e.g. 1 * 14 = 14 for a hard throw.
    // Scale that down so a rock is "a bit louder than a sprint, quieter than a gunshot".
    public static float RockImpactScale = 0.15f;

    public static void Emit(Vector3 position, float loudness, NoiseSource source)
    {
        if (loudness <= 0f) return;
        NoiseEmitted?.Invoke(new NoiseEvent(position, loudness, source));
    }

    // Clears listeners when Play mode starts (safe even with "Enter Play Mode Options"
    // that skip the domain reload, so we never keep listeners from a previous run).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        NoiseEmitted = null;
    }

    // Listen to the Systems Engineer's thrown rocks without editing their script.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookExternalSources()
    {
        Throwable.OnImpact -= OnRockImpact;   // never subscribe twice
        Throwable.OnImpact += OnRockImpact;
    }

    static void OnRockImpact(Vector3 position, float noise)
    {
        Emit(position, noise * RockImpactScale, NoiseSource.Impact);
    }
}
