public class InputBuffer
{
    private Enums.AttackInputs bufferedInput;
    private bool _hasInput = false;

    public bool HasInput => _hasInput;

    public void Register(Enums.AttackInputs input)
    {
        bufferedInput = input;
        _hasInput = true;
    }

    public bool TryConsume(out Enums.AttackInputs input)
    {
        if (!_hasInput)
        {
            input = default;
            return false;
        }

        input = bufferedInput;
        _hasInput = false;
        return true;
    }

    public void Clear()
    {
        _hasInput = false;
        bufferedInput = default;
    }
}
