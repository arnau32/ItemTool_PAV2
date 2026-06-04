using System.Collections.Generic;
using UnityEngine;

public class VFXRigContainer : MonoBehaviour, IVfxUser
{
    public List<GameObject> attackVfx = new();

    public virtual void ActivateAttack(int id)
    {
        if (id < 0 || id >= attackVfx.Count) return;
        attackVfx[id].SetActive(true);
    }

    public virtual void DeactiveAttack(int id)
    {
        if (id < 0 || id >= attackVfx.Count) return;
        attackVfx[id].SetActive(false);
    }
    
    public void DeactivateAll()
    {
        for (int i = 0; i < attackVfx.Count; i++)
        {
            var go = attackVfx[i];
            go.SetActive(false);
        }
    }
        
}
