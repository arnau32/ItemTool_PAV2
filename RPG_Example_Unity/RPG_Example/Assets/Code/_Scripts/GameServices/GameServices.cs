using System;
using System.Collections.Generic;

public static class GameServices
{
    private static readonly Dictionary<Type, IGameServices> _services     = new(16);
    private static readonly List<IGameServices> _shutdownList = new(16);
    
    // -- Registers a service
    public static void Register<T>(T service) where T : IGameServices
    {
        var type = typeof(T);

        if (_services.ContainsKey(type))
        {
            UnityEngine.Debug.LogWarning($"[GameServices] {type.Name} already registered. Overwriting.");
        }

        _services[type] = service;

        if (!_shutdownList.Contains(service))
        {
            _shutdownList.Add(service);
        }
    }
    
    //-- Gets a service from active services
    public static T Get<T>() where T : IGameServices
    {
        if (_services.TryGetValue(typeof(T), out var s)) return (T)s;

        throw new InvalidOperationException($"[GameServices] {typeof(T).Name} not found. Register it before calling Get<>.");
    }
    
    //-- Tries to get the service if exists returns true
    public static bool TryGet<T>(out T service) where T : IGameServices
    {
        if (_services.TryGetValue(typeof(T), out var s))
        {
            service = (T)s;
            return true;
        }

        service = default;
        return false;
    }
    
    // Lifecycle — called by GameBootstrap, not by consumers
    public static void InitializeAll()
    {
        foreach (var gameServices in _shutdownList)
        {
            if (gameServices is IInitializable init)
            {
                init.Initialize();
            }
        }
    }
    
    public static void ShutdownAll()
    {
        for (int i = _shutdownList.Count - 1; i >= 0; i--)
        {
            if (_shutdownList[i] is IShutdownable shut)
            {
                shut.Shutdown();
            }
        }
    }
    
    //-- Deletes a service from dictionary
    public static void Unregister<T>() where T : IGameServices
    {
        if (!_services.TryGetValue(typeof(T), out var s)) return;
        
        _shutdownList.Remove(s);
        _services.Remove(typeof(T));
    }
    
    //-- Clears all the services
    public static void ClearAll()
    {
        _services.Clear();
        _shutdownList.Clear();
    }
}
