using System;
using UnityEngine;
using Primordia;

/// <summary>Measured poses for the actual imported weapon handles and barrels.</summary>
public static class PrimordiaWeaponGripCalibration
{
    public readonly struct GripPose
    {
        public readonly Quaternion ModelRotation;
        public readonly Vector3 HandlePosition;
        public readonly Vector3 MuzzlePosition;
        public readonly float InwardOffset, ForwardOffset, FingerCurl, HeightOffset;
        public readonly float RingCurlScale, LittleCurlScale, ThumbCurlScale, HandPitch;
        public readonly Vector3 GripFingerAngles;
        public readonly float IndexCurlScale;
        public readonly Vector2 ThumbOpposition;

        public GripPose(Quaternion modelRotation, Vector3 handlePosition, Vector3 muzzlePosition,
            float inwardOffset = 0.0285f, float forwardOffset = 0.043f, float fingerCurl = 1f,
            float heightOffset = -0.003f, float ringCurlScale = 0.9f, float littleCurlScale = 0.75f,
            float handPitch = 0f, float thumbCurlScale = 1f, Vector3? gripFingerAngles = null,
            float indexCurlScale = 1f, Vector2? thumbOpposition = null)
        {
            ModelRotation = modelRotation;
            HandlePosition = handlePosition;
            MuzzlePosition = muzzlePosition;
            InwardOffset = inwardOffset;
            ForwardOffset = forwardOffset;
            FingerCurl = fingerCurl;
            HeightOffset = heightOffset;
            RingCurlScale = ringCurlScale;
            LittleCurlScale = littleCurlScale;
            HandPitch = handPitch;
            ThumbCurlScale = thumbCurlScale;
            GripFingerAngles = gripFingerAngles ?? new Vector3(35f, 55f, 40f);
            IndexCurlScale = indexCurlScale;
            ThumbOpposition = thumbOpposition ?? Vector2.zero;
        }
    }

    public static void ConfigureHand(GameObject weapon, string weaponName)
    {
        var pose = Get(weaponName);
        var grip = weapon.GetComponent<WeaponPalmGrip>();
        if (grip == null) grip = weapon.AddComponent<WeaponPalmGrip>();
        grip.Configure(pose.InwardOffset, pose.ForwardOffset, pose.FingerCurl,
            pose.HeightOffset, pose.RingCurlScale, pose.LittleCurlScale,
            pose.HandPitch, pose.ThumbCurlScale, pose.GripFingerAngles, pose.IndexCurlScale, pose.ThumbOpposition);
    }

    // Root +Z follows the visible barrel (or melee shaft); gun +Y is upright.
    // Handle positions are mesh cross-section centres, not whole-model bounds ratios.
    // The player supplies a separate mirrored palm-channel target for either hand.
    public static GripPose Get(string weaponName) => weaponName switch
    {
        "Scrap Revolver" => new GripPose(new Quaternion(0f, 0.707106781f, 0.707106781f, 0f),
            new Vector3(-0.001821f, -0.03f, -0.085517f), new Vector3(-0.001727f, 0.065144f, 0.142f),
            inwardOffset: 0.035f, heightOffset: 0.004f, forwardOffset: 0.035f, indexCurlScale: 0.4f),
        "Bone Sidearm" => new GripPose(new Quaternion(0f, -0.707106781f, 0f, 0.707106781f),
            new Vector3(-0.00045f, -0.0225f, -0.05758575f), new Vector3(-0.000678f, 0.05598825f, 0.099f),
            inwardOffset: 0.041f, heightOffset: -0.012f, forwardOffset: 0.027f, fingerCurl: 0.8f, thumbCurlScale: 0f, indexCurlScale: 0.75f, thumbOpposition: new Vector2(18.75f, 0f)),
        "Pump Carbine" => new GripPose(new Quaternion(0.01253265f, -0.70299076f, -0.01253265f, 0.7109781f),
            new Vector3(-0.00245955f, -0.06f, -0.2520114f), new Vector3(0.015665775f, 0.02399895f, 0.2939967f),
            inwardOffset: 0.032f, heightOffset: 0.023f, forwardOffset: 0.037f, thumbCurlScale: 0f, gripFingerAngles: new Vector3(0f, 90f, 40f), indexCurlScale: 0.55f, thumbOpposition: new Vector2(30f, 0f)),
        "Hunting Rifle" => new GripPose(new Quaternion(-0.707106781f, 0f, 0f, 0.707106781f),
            new Vector3(-0.014271f, -0.01f, -0.334304f), new Vector3(-0.0142713f, 0.0466789f, 0.502f),
            inwardOffset: 0.048f, heightOffset: 0.003f, forwardOffset: 0.02f, thumbCurlScale: 0f, handPitch: 30f, gripFingerAngles: new Vector3(10f, 90f, 60f), indexCurlScale: 0.75f),
        "Auto Carbine" => new GripPose(new Quaternion(-0f, -1f, -0f, 4.371139e-08f),
            new Vector3(-0.00428879233f, -0.0550027524f, -0.117747222f), new Vector3(-0.00470626128f, 0.0381522344f, 0.432000542f),
            inwardOffset: 0.03f, heightOffset: 0.01f, forwardOffset: 0.045f, handPitch: 30f, gripFingerAngles: new Vector3(20f, 90f, 60f)),
        "Plated Shotgun" => new GripPose(new Quaternion(-0f, -0.707106716f, -0f, 0.707106846f),
            new Vector3(1.67500002e-08f, 0.0244940788f, -0.240000033f), new Vector3(1.16750001e-07f, 0.0622104144f, 0.487f),
            inwardOffset: 0.041f, forwardOffset: 0.052f, thumbCurlScale: 0.5f, handPitch: 32f, gripFingerAngles: new Vector3(20f, 55f, 60f), indexCurlScale: 0.6f),
        "Marksman Rifle" => new GripPose(new Quaternion(0.0628410965f, 0f, 0f, 0.998023545f),
            new Vector3(0.00103109432f, -0.0353295314f, -0.337069661f), new Vector3(0.00045494094f, 0.0402184014f, 0.601941681f),
            inwardOffset: 0.0287f, heightOffset: 0.021f, forwardOffset: 0.045f, handPitch: 15f, gripFingerAngles: new Vector3(20f, 90f, 60f), indexCurlScale: 0.6f, thumbOpposition: new Vector2(0f, -15f)),
        "Apex Rifle" => new GripPose(new Quaternion(-0f, -0.707106716f, -0f, 0.707106846f),
            new Vector3(-5.51835506e-05f, -0.0500000006f, 0.0679873668f), new Vector3(-0.000759033559f, 0.0332571704f, 0.502f),
            inwardOffset: 0.026f, heightOffset: 0.026f, forwardOffset: 0.045f, thumbCurlScale: 0.5f, gripFingerAngles: new Vector3(20f, 90f, 60f), indexCurlScale: 0.6f),
        "Wooden Spear" => new GripPose(new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(-1.30000006e-09f, -1.9500001e-09f, -0.40000007f), new Vector3(0f, 0f, 0f),
            forwardOffset: 0.039f, gripFingerAngles: new Vector3(0f, 55f, 60f), thumbOpposition: new Vector2(-15f, 0f)),
        "Bone Club" => new GripPose(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f),
            new Vector3(-0.00672999841f, -0.000230011159f, -0.231199996f), new Vector3(0f, 0f, 0f),
            forwardOffset: 0.041f, gripFingerAngles: new Vector3(0f, 55f, 60f), thumbOpposition: new Vector2(0f, -30f)),
        "Claw Blade" => new GripPose(new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(-0.000789999986f, -0.000789998986f, -0.165600012f), new Vector3(0f, 0f, 0f),
            inwardOffset: 0.03075f, heightOffset: -0.025f, forwardOffset: 0.0325f, gripFingerAngles: new Vector3(0f, 55f, 60f), thumbOpposition: new Vector2(-30f, 0f)),
        "Horn Pike" => new GripPose(new Quaternion(0f, 0f, 0.707106781f, 0.707106781f),
            new Vector3(-0.000470014856f, -0.0249950327f, -0.410399912f), new Vector3(0f, 0f, 0f),
            inwardOffset: 0.0265f, forwardOffset: 0.039f, gripFingerAngles: new Vector3(0f, 55f, 60f), thumbOpposition: new Vector2(-15f, 0f)),
        "Spike Maul" => new GripPose(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f),
            new Vector3(0.00230000779f, 0.00272000784f, -0.259200032f), new Vector3(0f, 0f, 0f),
            inwardOffset: 0.0325f, forwardOffset: 0.039f, gripFingerAngles: new Vector3(0f, 55f, 60f), thumbOpposition: new Vector2(-15f, 0f)),
        "Plated Cleaver" => new GripPose(new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(-0.000150000006f, -0.0232780009f, -0.295200028f), new Vector3(0f, 0f, 0f),
            inwardOffset: 0.0325f, forwardOffset: 0.037f, gripFingerAngles: new Vector3(0f, 55f, 40f), thumbOpposition: new Vector2(-15f, 0f)),
        "Rex-Fang Greatblade" => new GripPose(new Quaternion(0f, 0f, 0.707106781f, 0.707106781f),
            new Vector3(0.00754000097f, 0f, -0.589000076f), new Vector3(0f, 0f, 0f),
            forwardOffset: 0.041f, gripFingerAngles: new Vector3(0f, 55f, 60f)),
        "Forged Longsword" => new GripPose(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f),
            new Vector3(0.012254f, -0.02967899f, -0.38f), new Vector3(0f, 0f, 0f),
            inwardOffset: 0.0305f, forwardOffset: 0.039f, gripFingerAngles: new Vector3(0f, 55f, 40f), thumbOpposition: new Vector2(-15f, 0f)),
        "Knife" => new GripPose(new Quaternion(0f, 0f, 0f, 1f),
            PlayerPunch.KnifeHandlePosition, new Vector3(0f, 0f, 0f),
            inwardOffset: 0.0245f, forwardOffset: 0.037f, littleCurlScale: 0.1f),
        _ => throw new InvalidOperationException($"No measured handle pose for {weaponName}."),
    };
}
