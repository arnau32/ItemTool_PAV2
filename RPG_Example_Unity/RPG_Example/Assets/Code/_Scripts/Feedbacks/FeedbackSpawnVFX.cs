using UnityEngine;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Instance")]
    public class FeedbackSpawnVFX : FeedbackBase
    {
        public GameObject prefab;
        public bool isPersistent = false;
        public float duration = 1f;

        [Tooltip("If assigned, the VFX spawns at this transform's position and rotation. " +
                 "Takes priority over Local Position Offset and Local Euler Offset.")]
        public Transform spawnTransform;

        [Tooltip("Local position offset relative to the owner transform. " +
                 "Only used when Spawn Transform is null.")]
        public Vector3 localPositionOffset;

        [Tooltip("Local euler rotation offset relative to the owner transform. " +
                 "Only used when Spawn Transform is null.")]
        public Vector3 localEulerOffset;

        private GameObject _instance;

        public override void Play(GameObject owner)
        {
            if (!active || prefab == null) return;

            if (isPersistent)
            {
                if (_instance != null) return;

                _instance = Spawn(owner);
            }
            else
            {
                var go = Spawn(owner);
                if (go != null)
                    GameObject.Destroy(go, duration);
            }
        }

        private GameObject Spawn(GameObject owner)
        {
            if (spawnTransform != null)
            {
                var go = GameObject.Instantiate(prefab, spawnTransform);
                go.transform.localPosition = localPositionOffset;
                go.transform.rotation = Quaternion.Euler(localEulerOffset);
                return go;
            }

            return GameObject.Instantiate(prefab, owner.transform.TransformPoint(localPositionOffset), Quaternion.Euler(localEulerOffset));
        }
    }
}