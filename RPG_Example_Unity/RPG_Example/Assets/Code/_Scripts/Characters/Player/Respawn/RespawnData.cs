public static class RespawnData
{
    public static int  SpawnPointIndex = 0;

    // True when the player just died and loaded back into the base scene.
    // Consumed and cleared by RevivalDialogueTrigger on scene load.
    public static bool CameFromDeath = false;

    // True by default (app start / main menu continue).
    // Set to false by death and extraction so PlayerSpawner uses a spawn point instead.
    // Reset back to true by PlayerSpawner after placing the player at a spawn point,
    // so subsequent close/reopen within Village resume from the last saved position.
    public static bool UseSavedPosition = true;
}