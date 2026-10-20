using UnityEngine;

// A gentle, occasional dip rather than rapid flashing.
[RequireComponent(typeof(Light))]
public class BunkerLight : MonoBehaviour
{
    public float interval = 9f;
    private Light lamp;
    private float originalIntensity;
    void Awake() { lamp = GetComponent<Light>(); originalIntensity = lamp.intensity; }
    void Update()
    {
        float cycle = Time.time % interval;
        lamp.intensity = originalIntensity * (cycle < 0.3f ? 0.72f : 1f);
    }
}
