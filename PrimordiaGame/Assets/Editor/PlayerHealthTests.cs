using NUnit.Framework;
using UnityEngine;

public class PlayerHealthTests
{
    GameObject player;
    PlayerHealth health;
    int damageEvents;
    int deathEvents;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Test Player");
        health = player.AddComponent<PlayerHealth>();
        health.RestoreHealth();
        health.damaged += _ => damageEvents++;
        health.died += () => deathEvents++;
        damageEvents = deathEvents = 0;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(player);

    [Test]
    public void InvulnerableByDefault_IgnoresDirectAndCombatHits()
    {
        Assert.That(health.invulnerable, Is.True);
        health.TakeDamage(25f);
        health.ApplyHit(new CombatHit(1000f, Vector3.zero, Vector3.up, null));

        Assert.That(health.CurrentHealth, Is.EqualTo(health.maxHealth));
        Assert.That(health.IsDead, Is.False);
        Assert.That(damageEvents, Is.Zero);
        Assert.That(deathEvents, Is.Zero);
    }

    [Test]
    public void DisablingInvulnerability_AllowsDamageAndDeath()
    {
        health.invulnerable = false;
        health.TakeDamage(25f);
        Assert.That(health.CurrentHealth, Is.EqualTo(75f));
        Assert.That(damageEvents, Is.EqualTo(1));

        health.ApplyHit(new CombatHit(75f, Vector3.zero, Vector3.up, null));
        Assert.That(health.IsDead, Is.True);
        Assert.That(deathEvents, Is.EqualTo(1));

        health.TakeDamage(10f);
        Assert.That(damageEvents, Is.EqualTo(2));
        Assert.That(deathEvents, Is.EqualTo(1));
    }

    [Test]
    public void EnablingInvulnerabilityDuringCombat_PreservesRemainingHealth()
    {
        health.invulnerable = false;
        health.TakeDamage(25f);
        health.invulnerable = true;
        health.TakeDamage(100f);

        Assert.That(health.CurrentHealth, Is.EqualTo(75f));
        Assert.That(health.IsDead, Is.False);
        Assert.That(damageEvents, Is.EqualTo(1));
        Assert.That(deathEvents, Is.Zero);
    }

    [Test]
    public void RestoreHealth_PreservesInspectorToggle()
    {
        health.invulnerable = false;
        health.TakeDamage(100f);
        health.RestoreHealth();

        Assert.That(health.CurrentHealth, Is.EqualTo(health.maxHealth));
        Assert.That(health.IsDead, Is.False);
        Assert.That(health.invulnerable, Is.False);

        health.invulnerable = true;
        health.RestoreHealth();
        Assert.That(health.invulnerable, Is.True);
    }

    [Test]
    public void InvulnerablePlayer_IgnoresCombatResolverHitThroughChildCollider()
    {
        var child = new GameObject("Player Hitbox");
        child.transform.SetParent(player.transform);
        var collider = child.AddComponent<BoxCollider>();
        Assert.That(CombatDamageResolver.TryApply(collider,
            new CombatHit(1000f, Vector3.zero, Vector3.up, null), out var target), Is.True);
        Assert.That(target, Is.SameAs(health));
        Assert.That(health.CurrentHealth, Is.EqualTo(health.maxHealth));
        Assert.That(health.IsDead, Is.False);
        Assert.That(damageEvents, Is.Zero);
    }
}
