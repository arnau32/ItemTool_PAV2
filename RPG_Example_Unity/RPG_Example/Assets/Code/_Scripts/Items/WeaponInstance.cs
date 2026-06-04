using System;
using System.Collections.Generic;
using UnityEngine;
using FeedbacksNagu;

namespace Gameplay.Items
{
    /// Pairs a collider slot type with its handler.
    [Serializable]
    public class SlotColliderPair
    {
        public Enums.ColliderSlot slot;
        public AttackColliderHandler collider;
    }

    public class WeaponInstance : MonoBehaviour
    {
        [Header("Base Data")] public WeaponData data;
        [SerializeField] private WeaponAudio _weaponAudio;

        [Header("Attack Colliders")] public List<SlotColliderPair> slotColliders;

        [Header("Parry Collider")] public ParryColliderHandler parryCollider;

        [Header("Weapon VFX")] [SerializeField]
        private WeaponVfxController weaponVfx;

        [Header("Quality VFX")]
        [Tooltip("Child MeshRenderer (same mesh as weapon, inactive by default). Uses WeaponQualityRimGlow shader.")]
        [SerializeField] private GameObject[] _qualityVfxObject;

        [SerializeField] private Enums.WeaponHand _hand = Enums.WeaponHand.Right;

        private static readonly int PropQualityColor  = Shader.PropertyToID("_QualityColor");
        private static readonly int PropBaseColor     = Shader.PropertyToID("_BaseColor");
        private static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");

        private Dictionary<Enums.ColliderSlot, AttackColliderHandler> _colliderMap;
        private List<AttackColliderHandler> _colliderHandlers;
        private MaterialPropertyBlock _qualityMpb;

        public event Action<DamageContext> OnDealtDamage;
        public event Action<AttackColliderHandler> OnParryContact;
        public event Action<EnemyProjectile> OnProjectileParryContact;

        [Header("Feedbacks")] public FeedbackContainer onAttackStarted;
        public FeedbackContainer onHit;

        public Enums.WeaponHand Hand => _hand;

        #region Unity Callbacks

        private void Awake()
        {
            if (_weaponAudio == null)
                _weaponAudio = GetComponent<WeaponAudio>();

            int count = slotColliders != null ? slotColliders.Count : 0;
            _colliderMap = new Dictionary<Enums.ColliderSlot, AttackColliderHandler>(count);
            _colliderHandlers = new List<AttackColliderHandler>(count);

            for (int i = 0; i < count; i++)
            {
                var pair = slotColliders[i];
                if (pair == null || pair.collider == null) continue;

                // TryAdd: single lookup, skips duplicates without a second ContainsKey call.
                if (_colliderMap.TryAdd(pair.slot, pair.collider))
                    _colliderHandlers.Add(pair.collider);

                pair.collider.OnDealDamage += HandleColliderDealDamage;
            }

            if (parryCollider != null)
            {
                parryCollider.OnParryContact += HandleParryContact;
                parryCollider.OnProjectileParryContact += HandleProjectileParryContact;
            }

            _qualityMpb = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _colliderHandlers.Count; i++)
            {
                _colliderHandlers[i].OnDealDamage -= HandleColliderDealDamage;
            }

            if (parryCollider != null)
            {
                parryCollider.OnParryContact -= HandleParryContact;
                parryCollider.OnProjectileParryContact -= HandleProjectileParryContact;
            }
        }

        #endregion

        #region Public API

        public void ConfigureForOwner(Transform ownerRoot, Enums.Faction faction)
        {
            var layer = DamageRules.GetWeaponLayer(faction);
            if (layer >= 0)
                SetLayerRecursively(gameObject, layer);

            var attacker = ownerRoot != null ? ownerRoot : transform;

            for (int i = 0; i < _colliderHandlers.Count; i++)
                _colliderHandlers[i].ConfigureOwner(attacker, this, faction);

            if (parryCollider != null)
                parryCollider.ConfigureOwner(attacker, faction);
        }

        public void SetHand(Enums.WeaponHand hand) => _hand = hand;

        public void SetWeaponVfx(int vfxId, bool enabled)
        {
            if (weaponVfx == null) return;
            weaponVfx.SetVfx(vfxId, enabled);
        }

        public void DisableAllWeaponVfx()
        {
            if (weaponVfx == null) return;
            weaponVfx.SetAll(false, clear: false);
        }

        public void OnAttackStarted()
        {
            onAttackStarted.PlayFeedbacks(gameObject);
        }

        public AttackColliderHandler GetCollider(Enums.ColliderSlot slot)
        {
            _colliderMap.TryGetValue(slot, out var col);
            return col;
        }

        public void EnableSlot(Enums.ColliderSlot slot)
        {
            if (_colliderMap.TryGetValue(slot, out var col))
                col.ActivateCollider();
        }

        public void DisableSlot(Enums.ColliderSlot slot)
        {
            if (_colliderMap.TryGetValue(slot, out var col))
                col.DeactivateCollider();
        }

        public void EnableParryCollider()
        {
            if (parryCollider != null)
                parryCollider.ActivateCollider();
        }

        public void DisableParryCollider()
        {
            if (parryCollider != null)
                parryCollider.DeactivateCollider();
        }

        public void ApplyQualityColor(Color color)
        {
            if (_qualityVfxObject == null) return;

            for (int i = 0; i < _qualityVfxObject.Length; i++)
            {
                if (_qualityVfxObject[i] == null) continue;
                _qualityVfxObject[i].SetActive(true);

                var renderers = _qualityVfxObject[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    _qualityMpb.Clear();
                    _qualityMpb.SetColor(PropQualityColor,  color);
                    _qualityMpb.SetColor(PropBaseColor,     color);
                    _qualityMpb.SetColor(PropEmissionColor, color);
                    renderers[r].SetPropertyBlock(_qualityMpb);
                }

                var systems = _qualityVfxObject[i].GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    var main = systems[s].main;
                    main.startColor = new ParticleSystem.MinMaxGradient(color);
                }

                var lights = _qualityVfxObject[i].GetComponentsInChildren<Light>(true);
                for (int l = 0; l < lights.Length; l++)
                    lights[l].color = color;
            }
        }

        #endregion

        #region Helpers

        private void HandleColliderDealDamage(DamageContext ctx)
        {
            _weaponAudio?.PlayImpact(WeaponImpactType.Flesh);
            OnDealtDamage?.Invoke(ctx);
            onHit.PlayFeedbacks(gameObject);
        }

        private void HandleParryContact(AttackColliderHandler enemyAttackCollider)
        {
            OnParryContact?.Invoke(enemyAttackCollider);
        }

        private void HandleProjectileParryContact(EnemyProjectile projectile)
        {
            OnProjectileParryContact?.Invoke(projectile);
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            var t = obj.transform;
            int childCount = t.childCount;

            for (int i = 0; i < childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        #endregion
    }
}