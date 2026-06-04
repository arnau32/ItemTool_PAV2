
public interface ISaveable
{
    void CaptureToSave(SaveData data);

    void ApplyFromSave(SaveData data);
}