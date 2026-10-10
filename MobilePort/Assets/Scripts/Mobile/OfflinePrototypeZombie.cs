using System;
using UnityEngine;

/// <summary>Simple local target to test hits, death and DEV respawning.</summary>
public class OfflinePrototypeZombie : MonoBehaviour
{
    public Transform player;
    public int health = 100;
    public float moveSpeed = 1.25f;
    public float stopDistance = 1.7f;
    public Action onDeath;

    public int Health { get { return Mathf.Max(0, health); } }
    public bool IsDead { get; private set; }

    private void Update()
    {
        if (IsDead || player == null) return;
        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > stopDistance * stopDistance)
        {
            transform.position += delta.normalized * moveSpeed * Time.deltaTime;
            if (delta.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(delta), Time.deltaTime * 5f);
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        health -= Mathf.CeilToInt(amount);
        if (health > 0) return;

        IsDead = true;
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null) renderer.material.color = Color.gray;
        onDeath?.Invoke();
    }
}
