using UnityEngine;

public enum WeaponType { Melee, Ranged }

public class PlayerStats : MonoBehaviour
{
    public float maxHealth = 100f;
    public float health = 100f;
    public WeaponType weapon = WeaponType.Ranged;
    public float HealthFraction => health / maxHealth;   // 0 to 1, what BossBrain expects

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) weapon = WeaponType.Melee;
        if (Input.GetKeyDown(KeyCode.Alpha2)) weapon = WeaponType.Ranged;

        // Simple shooting: left click fires a ray from the camera
        if (Input.GetMouseButtonDown(0) && weapon == WeaponType.Ranged &&
            Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 50f))
        {
            var boss = hit.collider.GetComponent<BossHealth>();
            if (boss != null) boss.TakeDamage(10f);
        }
    }
}