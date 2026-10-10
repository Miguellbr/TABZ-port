using System;
using UnityEngine;

/// <summary>
/// Local hitscan weapon for validating the first combat loop offline.
/// Replace this with the recovered TABZ Weapon component once its prefab and
/// Photon-independent fire path are reconstructed.
/// </summary>
public class OfflinePrototypeWeapon : MonoBehaviour
{
    public Camera viewCamera;
    public int magazineSize = 30;
    public int ammo = 30;
    public int reserveAmmo = 90;
    public float damage = 34f;
    public float range = 100f;
    public float roundsPerSecond = 8f;
    public Func<OfflinePrototypeZombie> targetProvider;

    private float nextShot;

    public int Ammo { get { return ammo; } }
    public int ReserveAmmo { get { return reserveAmmo; } }

    private void Update()
    {
        bool fireHeld = MobileInputState.FireHeld;
        bool firePressed = MobileInputState.FirePressed;
        bool reload = MobileInputState.ReloadPressed;

        if (!Application.isMobilePlatform)
        {
            fireHeld |= Input.GetMouseButton(0);
            firePressed |= Input.GetMouseButtonDown(0);
            reload |= Input.GetKeyDown(KeyCode.R);
        }

        if (reload) Reload();
        if ((fireHeld || firePressed) && Time.time >= nextShot)
        {
            if (ammo > 0) Fire();
            else nextShot = Time.time + 0.25f;
        }

        MobileInputState.FirePressed = false;
        MobileInputState.ReloadPressed = false;
    }

    private void Fire()
    {
        nextShot = Time.time + 1f / Mathf.Max(1f, roundsPerSecond);
        ammo--;
        if (viewCamera == null) return;

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, range))
        {
            OfflinePrototypeZombie enemy = hit.collider.GetComponentInParent<OfflinePrototypeZombie>();
            if (enemy != null) enemy.TakeDamage(damage);
        }
    }

    public void Reload()
    {
        if (ammo >= magazineSize || reserveAmmo <= 0) return;
        int amount = Mathf.Min(magazineSize - ammo, reserveAmmo);
        ammo += amount;
        reserveAmmo -= amount;
    }
}
