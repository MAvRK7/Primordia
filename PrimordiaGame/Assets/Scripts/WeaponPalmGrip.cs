using UnityEngine;

namespace Primordia
{
    /// <summary>Seats an authored handle in the fingers and limits curl for thick grips.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponPalmGrip : MonoBehaviour
    {
        [SerializeField, Min(0f)] float m_InwardOffset = 0.0285f;
        [SerializeField] float m_HeightOffset = -0.003f;
        [SerializeField, Min(0f)] float m_ForwardOffset = 0.043f;
        [SerializeField, Range(0f, 1f)] float m_FingerCurl = 1f;
        [SerializeField, Range(0f, 1f)] float m_RingCurlScale = 0.9f;
        [SerializeField, Range(0f, 1f)] float m_LittleCurlScale = 0.75f;
        [SerializeField, Range(0f, 1f)] float m_ThumbCurlScale = 1f;
        [SerializeField, Range(-60f, 60f)] float m_HandPitch;
        [SerializeField] Vector3 m_GripFingerAngles = new Vector3(35f, 55f, 40f);
        [SerializeField, Range(0f, 1f)] float m_IndexCurlScale = 1f;
        [SerializeField] Vector2 m_ThumbOpposition;

        public float FingerCurl => m_FingerCurl;
        public float RingCurlScale => m_RingCurlScale;
        public float LittleCurlScale => m_LittleCurlScale;
        public float ThumbCurlScale => m_ThumbCurlScale;
        public float HandPitch => m_HandPitch;
        public Vector3 GripFingerAngles => m_GripFingerAngles;
        public float IndexCurlScale => m_IndexCurlScale;
        public Vector2 ThumbOpposition => m_ThumbOpposition;
        public Vector3 GetPalmOffset(bool isLeftHand) =>
            new Vector3(isLeftHand ? m_InwardOffset : -m_InwardOffset, m_HeightOffset, m_ForwardOffset);

        public void Configure(float inwardOffset, float forwardOffset, float fingerCurl,
            float heightOffset = -0.003f, float ringCurlScale = 0.9f, float littleCurlScale = 0.75f,
            float handPitch = 0f, float thumbCurlScale = 1f, Vector3? gripFingerAngles = null,
            float indexCurlScale = 1f, Vector2? thumbOpposition = null)
        {
            m_InwardOffset = inwardOffset;
            m_HeightOffset = heightOffset;
            m_ForwardOffset = forwardOffset;
            m_FingerCurl = fingerCurl;
            m_RingCurlScale = ringCurlScale;
            m_LittleCurlScale = littleCurlScale;
            m_HandPitch = handPitch;
            m_ThumbCurlScale = thumbCurlScale;
            m_GripFingerAngles = gripFingerAngles ?? new Vector3(35f, 55f, 40f);
            m_IndexCurlScale = indexCurlScale;
            m_ThumbOpposition = thumbOpposition ?? Vector2.zero;
        }

        public static Pose RecoilVisualPose(Vector3 restPosition, Quaternion restRotation,
            Vector3 gripPivot, float angle)
        {
            var rotation = Quaternion.AngleAxis(angle, Vector3.right);
            return new Pose(gripPivot + rotation * (restPosition - gripPivot), rotation * restRotation);
        }
    }
}
