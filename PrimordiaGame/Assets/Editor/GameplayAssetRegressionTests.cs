using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

public class GameplayAssetRegressionTests
{
    [Test]
    public void ControllerHandsUseGripPoses_WhileRaysUseSeparateAimPoses()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Primordia Player.prefab");
        Assert.That(player, Is.Not.Null);
        foreach (var side in new[] { "Left", "Right" })
        {
            var controller = player.GetComponentsInChildren<Transform>(true).Single(t => t.name == side + " Controller");
            var driver = controller.GetComponent<TrackedPoseDriver>();
            Assert.That(driver.positionInput.action.bindings.Single().path, Is.EqualTo($"<XRController>{{{side}Hand}}/devicePosition"));
            Assert.That(driver.rotationInput.action.bindings.Single().path, Is.EqualTo($"<XRController>{{{side}Hand}}/deviceRotation"));
            var aim = controller.parent.Find(side + " Controller Aim Pose");
            Assert.That(aim, Is.Not.Null);
            Assert.That(aim.GetComponent<TrackedPoseDriver>().positionInput.reference, Is.Not.Null);
            foreach (var ray in controller.GetComponentsInChildren<CurveInteractionCaster>(true))
                Assert.That(ray.castOrigin, Is.EqualTo(aim));
        }
    }

    [Test]
    public void HandPalmsAreCenteredAtGrip_WithoutMirroring()
    {
        foreach (var side in new[] { "Left", "Right" })
        {
            var hand = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ThirdParty/LowPolyHand/Prefabs/{side} Controller Hand.prefab");
            var palm = hand.GetComponentsInChildren<Transform>(true).Single(t => t.name == side[0] + "_Palm");
            Assert.That(hand.transform.InverseTransformPoint(palm.position).magnitude, Is.LessThan(0.00001f));
            Assert.That(hand.GetComponentsInChildren<Transform>(true).All(t => t.localScale.x > 0f && t.localScale.y > 0f && t.localScale.z > 0f), Is.True);
        }
    }

    [Test]
    public void GrasslandSpawnsPlayableDinos_WithCompatibleNavigation()
    {
        var table = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Project/Data/Spawning/GrasslandSpawnTable.asset");
        Assert.That(table.entries.Count, Is.EqualTo(6));
        foreach (var entry in table.entries)
        {
            Assert.That(entry.prefab, Is.Not.Null);
            Assert.That(entry.prefab.GetComponent<DinoAI>()?.profile, Is.Not.Null);
            int type = entry.prefab.GetComponent<NavMeshAgent>().agentTypeID;
            Assert.That(NavMesh.GetSettingsByID(type).agentTypeID, Is.EqualTo(type));
            Assert.That(type, Is.EqualTo(table.entries[0].prefab.GetComponent<NavMeshAgent>().agentTypeID));
        }
    }

    [Test]
    public void PlayerPrefabStartsInvulnerable_WithFloorTracking()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Primordia Player.prefab");
        Assert.That(player.GetComponentInChildren<PlayerHealth>(true).invulnerable, Is.True);
        Assert.That((int)player.GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true).RequestedTrackingOriginMode, Is.EqualTo(2));
    }
}
