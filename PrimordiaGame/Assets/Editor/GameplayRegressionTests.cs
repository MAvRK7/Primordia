using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class GameplayRegressionTests
{
    readonly List<Object> owned = new();
    NavMeshDataInstance navigation;
    GameObject player;
    PlayerHealth health;
    Transform head;

    [SetUp]
    public void SetUp()
    {
        var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
            transform = Matrix4x4.TRS(Vector3.down * 0.5f, Quaternion.identity, Vector3.one),
            size = new Vector3(160f, 1f, 160f), area = 0 };
        var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),
            new List<NavMeshBuildSource> { source }, new Bounds(Vector3.zero, new Vector3(170f, 10f, 170f)),
            Vector3.zero, Quaternion.identity);
        owned.Add(data);
        navigation = NavMesh.AddNavMeshData(data);
        player = Create("Player");
        player.tag = "Player";
        health = player.AddComponent<PlayerHealth>();
        health.RestoreHealth();
        head = new GameObject("Head").transform;
        head.SetParent(player.transform);
        head.localPosition = Vector3.up * 1.7f;
        head.gameObject.AddComponent<Camera>();
    }

    [TearDown]
    public void TearDown()
    {
        navigation.Remove();
        for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    GameObject Create(string name)
    {
        var go = new GameObject(name);
        owned.Add(go);
        return go;
    }

    DinoAI Dino(DinoBehaviour behaviour = DinoBehaviour.PredatorHuntsHerbivores)
    {
        var go = Create("Test Dino");
        go.transform.position = new Vector3(0f, 0f, 15f);
        go.AddComponent<NavMeshAgent>();
        var profile = ScriptableObject.CreateInstance<DinoProfile>();
        owned.Add(profile);
        profile.behaviour = behaviour;
        profile.prefersPlayer = false;
        profile.leashRange = 35f;
        var ai = go.AddComponent<DinoAI>();
        ai.profile = profile;
        Call(ai, "Start");
        return ai;
    }

    public static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    [Test]
    public void PlayerTarget_FollowsRoomScaleHeadMovement()
    {
        head.localPosition += Vector3.right * 3f;
        Assert.That(PlayerHealth.ResolveTarget(player.transform), Is.EqualTo(head));
        Assert.That(PlayerHealth.ResolveTarget(head), Is.EqualTo(head));
    }

    [Test]
    public void PredatorRetaliatesAgainstControllerChild_AndRalliesPack()
    {
        var ai = Dino();
        var controller = new GameObject("Controller").transform;
        controller.SetParent(player.transform);
        ai.pack = Create("Pack").AddComponent<RaptorPack>();
        ai.OnAttacked(controller);
        Assert.That(Call(ai, "ChoosePredatorTarget"), Is.EqualTo(head));
        Assert.That(ai.pack.SharedTarget, Is.EqualTo(head));
        Call(ai, "Think");
        Assert.That(ai.InCombat, Is.True);
    }

    [Test]
    public void PredatorRejectsNullAttacker()
    {
        var ai = Dino();
        ai.OnAttacked(null);
        var target = typeof(DinoAI).GetField("retaliationTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(target.GetValue(ai), Is.Null);
    }

    [Test]
    public void WanderPointsStayNearSpawn_AfterDinoDriftsAway()
    {
        var ai = Dino();
        var spawn = ai.transform.position;
        ai.transform.position = spawn + Vector3.right * 50f;
        for (int i = 0; i < 20; i++)
            Assert.That(Vector3.Distance((Vector3)Call(ai, "RandomWanderPoint"), spawn), Is.LessThan(20f));
    }

    [Test]
    public void CombatDistance_RespectsPrefabScale()
    {
        var ai = Dino();
        ai.profile.attackRange = 3f;
        ai.GetComponent<NavMeshAgent>().radius = 4f;
        ai.transform.localScale = Vector3.one * 0.35f;
        var controller = player.AddComponent<CharacterController>();
        controller.radius = 0.1f;
        Assert.That(ai.CombatDistance(head), Is.EqualTo(2.55f).Within(0.001f));
    }

    [Test]
    public void AttackCannotDamagePlayerOutsideCombatRange()
    {
        var ai = Dino();
        health.invulnerable = false;
        Assert.That(ai.GetComponent<DinoAttack>().TryAttack(head), Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(100f));
    }

    [Test]
    public void EngagedDinoStaysActiveBeyondManagerDistanceAndCap()
    {
        var manager = Create("Manager").AddComponent<DinoAIManager>();
        Call(manager, "Awake");
        manager.activeDistance = 1f;
        manager.maxActive = 1;
        manager.player = head;
        var ai = Dino();
        manager.Register(ai);
        ai.OnAttacked(player.transform);
        Call(ai, "Think");
        Call(manager, "Reevaluate");
        Assert.That(ai.InCombat, Is.True);
        Assert.That(manager.IsActive(ai), Is.True);
    }

    [Test]
    public void CampSheltersSpawnButNotNearbyRaptors()
    {
        var camp = Create("Camp");
        camp.transform.position = new Vector3(176f, 7.274693f, 154f);
        var zone = camp.AddComponent<CampsiteSafeZone>();
        Call(zone, "OnEnable");
        Assert.That(CampsiteSafeZone.Contains(camp.transform.position), Is.True);
        Assert.That(CampsiteSafeZone.Contains(new Vector3(188.27f, 7.17f, 146.09f)), Is.False);
        Assert.That(CampsiteSafeZone.Contains(new Vector3(191.78f, 6.84f, 147.29f)), Is.False);
    }

    [Test]
    public void KillingWithControllerChildAwardsXP()
    {
        var ai = Dino();
        var dinoHealth = ai.gameObject.AddComponent<DinoHealth>();
        dinoHealth.profile = ai.profile;
        Call(dinoHealth, "Start");
        var progression = player.AddComponent<PlayerProgression>();
        var controller = new GameObject("Controller").transform;
        controller.SetParent(player.transform);
        dinoHealth.TakeDamage(10000f, controller);
        Assert.That(dinoHealth.IsDead, Is.True);
        Assert.That(progression.currentXP, Is.EqualTo(ai.profile.xpReward));
    }

    [Test]
    public void ResourceSackRejectsNegativePrices()
    {
        var sack = Create("Sack").AddComponent<ResourceSack>();
        var meat = sack.meat;
        Assert.That(sack.SpendResources(-5, 0), Is.False);
        Assert.That(sack.meat, Is.EqualTo(meat));
    }

    [Test]
    public void NearbyPopulationStartsWithoutFullRespawnDelay()
    {
        var director = Create("Director").AddComponent<SpawnDirector>();
        director.initialSpawnDelay = 2f;
        director.respawnDelayMin = director.respawnDelayMax = 90f;
        Assert.That((float)Call(director, "RollSlotSpawnTime"), Is.EqualTo(Time.time + 2f).Within(0.001f));
    }
}
