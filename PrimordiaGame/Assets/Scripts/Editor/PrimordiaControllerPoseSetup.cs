using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

/// <summary>Uses grip positions, with aim rotations for visual hands and pointing rays.</summary>
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
            // Track the palm frame separately from the handle channel. Keep
            // anatomical wrist roll on the mesh and aim rotation on the item.
            var itemPose = controller.parent.Find(side + " Controller Item Pose");
            if (itemPose == null)
            {
                itemPose = new GameObject(side + " Controller Item Pose").transform;
                itemPose.SetParent(controller.parent, false);
            }
            var itemDriver = itemPose.GetComponent<TrackedPoseDriver>();
            if (itemDriver == null) itemDriver = itemPose.gameObject.AddComponent<TrackedPoseDriver>();
            itemDriver.positionInput = new InputActionProperty(new InputAction("Item Grip Position",
                InputActionType.PassThrough, $"<XRController>{{{side}Hand}}/devicePosition",
                expectedControlType: "Vector3"));
            itemDriver.rotationInput = aim.GetComponent<TrackedPoseDriver>().rotationInput;
            itemDriver.trackingStateInput = driver.trackingStateInput;
            itemDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            var palmGrip = itemPose.Find("Palm Grip Anchor");
            if (palmGrip == null)
            {
                palmGrip = new GameObject("Palm Grip Anchor").transform;
                palmGrip.SetParent(itemPose, false);
            }
            palmGrip.SetLocalPositionAndRotation(
                Primordia.ControllerHandVisual.PalmGripOffset(side == "Left"), Quaternion.identity);
            foreach (var attachment in controller.GetComponentsInChildren<InteractionAttachController>(true))
                attachment.transformToFollow = palmGrip;
            foreach (var caster in controller.GetComponentsInChildren<CurveInteractionCaster>(true))
                caster.castOrigin = aim;
            var punch = controller.GetComponent<PlayerPunch>();
            if (punch != null)
            {
                var serializedPunch = new SerializedObject(punch);
                serializedPunch.FindProperty("aimOrigin").objectReferenceValue = aim;
                serializedPunch.FindProperty("palmGripOrigin").objectReferenceValue = palmGrip;
                serializedPunch.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var hand in controller.GetComponentsInChildren<Primordia.ControllerHandVisual>(true))
            {
                var serializedHand = new SerializedObject(hand);
                serializedHand.FindProperty("m_AimRotationSource").objectReferenceValue = aim;
                serializedHand.FindProperty("m_PalmGripAnchor").objectReferenceValue = palmGrip;
                serializedHand.ApplyModifiedPropertiesWithoutUndo();
            }
            var ammoDisplay = controller.GetComponent<HeldAmmoDisplay>();
            if (ammoDisplay == null) ammoDisplay = controller.gameObject.AddComponent<HeldAmmoDisplay>();
            ammoDisplay.Configure(palmGrip, root.GetComponentInChildren<Camera>(true));
            EditorUtility.SetDirty(ammoDisplay);
            foreach (var image in controller.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.sprite != null) continue;
                string spritePath = image.name switch
                {
                    "Radial" => "UI/Joystick BG.png",
                    "Radial Icon" => "Icons/Chevron.png",
                    "Click" => "UI/Circle_60x60 Outline.png",
                    "Icon" when image.transform.parent.name == "Click" => "UI/CircleMask.png",
                    _ => null
                };
                if (spritePath != null)
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/VRTemplateAssets/Sprites/" + spritePath);
            }
        }

        var health = root.GetComponentInChildren<PlayerHealth>(true);
        var leftController = root.GetComponentsInChildren<Transform>(true)
            .Single(t => t.name == "Left Controller");
        var leftHand = leftController.GetComponentInChildren<Primordia.ControllerHandVisual>(true);
        if (health != null && leftHand != null)
        {
            var display = health.GetComponent<WristHealthDisplay>();
            if (display == null) display = health.gameObject.AddComponent<WristHealthDisplay>();
            display.Configure(health, leftHand.transform, health.GetComponentInChildren<Camera>(true));
            EditorUtility.SetDirty(display);
        }
    }
}
