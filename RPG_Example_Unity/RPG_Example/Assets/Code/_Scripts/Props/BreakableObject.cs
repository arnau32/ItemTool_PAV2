using UnityEngine;
using FeedbacksNagu;

public class BreakableObject : MonoBehaviour
{
    [SerializeField] private LayerMask layerToBreak;

    [SerializeField] private Rigidbody[] _rigidbodies;
    [SerializeField] private float _radius = 2f;
    [SerializeField] private float _force = 5f;
    [SerializeField] private Transform _pointToApplyForce;
    [SerializeField] private GameObject _mainCollider;

    [SerializeField] private FeedbackContainer _onBreakFeedback;

    private bool _broken;

    private void OnTriggerEnter(Collider other)
    {
        if (_broken) return;
        if (((1 << other.gameObject.layer) & layerToBreak.value) == 0) return;

        _broken = true;

        if (_mainCollider != null) _mainCollider.SetActive(false);

        foreach (var rb in _rigidbodies)
        {
            rb.isKinematic = false;
            rb.AddExplosionForce(_force, _pointToApplyForce.position, _radius);
            Physics.IgnoreCollision(rb.GetComponent<Collider>(), other, true);
        }

        _onBreakFeedback.PlayFeedbacks(gameObject);
    }
}
