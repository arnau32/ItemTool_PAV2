using System;
using FMODUnity;

[Serializable]
public struct AudioWindow
{
    public EventReference sound;
    public WindowEvent window;
    public bool stopImmediate;
}
