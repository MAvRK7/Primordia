using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

/// <summary>Tracks hands and held objects from grip poses while preserving aim poses for rays.</summary>
public static class PrimordiaControllerPoseSetup
{
    [MenuItem("Tools/Primordia/Player/Fix Controller Grip Poses")]
    public static void FixPlayerPrefab()
    {
        const string path = "Assets/Prefabs/Primordia Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Configure(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Configure(GameObject root)
    {
        foreach (var side in new[] { "Left", "Right" })
        {
            var controller = root.GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == side + " Controller");
            var driver = controller.GetComponent<TrackedPoseDriver>();
            var aim = controller.parent.Find(side + " Controller Aim Pose");
            if (aim == null)
            {
                aim = new GameObject(side + " Controller Aim Pose").transform;
                // Both poses are tracking-space coordinates; parenting aim under grip
                // would apply the controller transform twice.
                aim.SetParent(controller.parent, false);
                var aimDriver = aim.gameObject.AddComponent<TrackedPoseDriver>();
                aimDriver.positionInput = driver.positionInput;
                aimDriver.rotationInput = driver.rotationInput;
                aimDriver.trackingStateInput = driver.trackingStateInput;
                aimDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            }
            driver.positionInput = new InputActionProperty(new InputAction("Grip Position",
                InputActionType.PassThrough, $"<XRController>{{{side}Hand}}/devicePosition",
                expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Grip Rotation",
                InputActionType.PassThrough, $"<XRController>{{{side}Hand}}/deviceRotation",
                expectedControlType: "Quaternion"));
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            foreach (var caster in controller.GetComponentsInChildren<CurveInteractionCaster>(true))
                caster.castOrigin = aim;
        }
    }
}
