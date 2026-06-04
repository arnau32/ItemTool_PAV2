using UnityEngine;
using System.Collections.Generic;

//-- This manages slow motion requests for feedbacks or whatever we want

public class TimeScaleManager : IGameServices, IShutdownable
{
    private class SlowRequest
    {
        public string Id;
        public float TimeScale;
        public SlowRequest(string id, float timeScale)
        {
            Id = id;
            TimeScale = timeScale;
        }
    }

    private readonly List<SlowRequest> _requests = new(8);

    public void RequestSlow(string id, float scale)
    {
        RemoveById(id);
        _requests.Add(new SlowRequest(id, Mathf.Clamp01(scale)));
        Apply();
    }
    
    public void ReleaseSlow(string id)
    {
        RemoveById(id);
        Apply();
    }
    
    public void Shutdown()
    {
        _requests.Clear();
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
    
    private void RemoveById(string id)
    {
        for (var i = _requests.Count - 1; i >= 0; i--)
        {
            if (_requests[i].Id != id) continue;
            
            _requests.RemoveAt(i);
            return; // ids are unique — stop after first match
        }
    }

    private void Apply()
    {
        float min = 1f;

        for (int i = 0; i < _requests.Count; i++)
            min = Mathf.Min(min, _requests[i].TimeScale);

        Time.timeScale      = min;
        Time.fixedDeltaTime = 0.02f * min;
    }
}
