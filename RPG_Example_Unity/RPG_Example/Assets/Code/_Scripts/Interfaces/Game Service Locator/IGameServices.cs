// -- Game Services Interfaces

public interface IGameService { }

public interface IInitializable { void Initialize(); }

public interface IShutdownable { void Shutdown(); }

public interface ISceneReady { void OnSceneReady(); }

public interface ISceneCleanup { void OnSceneCleanup(); }

public interface IGameServices : IGameService { }