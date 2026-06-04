using UnityEngine;
using UnityEngine.UI;

public class GDTFadeEffect : MonoBehaviour
{
    public bool playOnAwake = true;
    public Color firstColor;
    public Color lastColor;
    public float timeEffect;
    public float initialDelay;
    public bool firstToLast = true;
    public bool pingPong;
    public float pingPongDelay;
    public bool disableWhenFinish = true;
    public float disableDelay;
    private float speed;
    public Image blackImage;
    private float currentValue;
    private bool performEffect = false;
    private bool finished = false;
    private bool halfCycle;
    private bool goingToLast;
    public bool loop = false;
    public bool deactiveParent = false;
    public bool reset = false;

    void OnEnable()
    {
        halfCycle = false;
        speed = 1 / timeEffect;
        goingToLast = firstToLast;
        if (blackImage == null)
        {
            blackImage = GetComponent<Image>();
        }

        if (firstToLast)
        {
            currentValue = 0f;
            blackImage.color = firstColor;
        }
        else
        {
            currentValue = 1f;
            blackImage.color = lastColor;
        }

        if (playOnAwake)
        {
            if (!performEffect)
            {
                Invoke("StartEffect", initialDelay);
            }
        }
    }

    void FixedUpdate()
    {
        if (performEffect)
        {
            if (pingPong)
            {
                if (!halfCycle)
                {
                    if (goingToLast)
                    {
                        if (PerformFadeIn())
                        {
                            Invoke("HalfCycleDelay", pingPongDelay);
                        }
                    }
                    else
                    {
                        if (PerformFadeOut())
                        {
                            Invoke("HalfCycleDelay", pingPongDelay);
                        }
                    }
                }
                else
                {
                    if (goingToLast)
                    {
                        if (PerformFadeIn())
                        {
                            if (!loop)
                            {
                                performEffect = false;
                            }

                            finished = true;
                        }
                    }
                    else
                    {
                        if (PerformFadeOut())
                        {
                            finished = true;
                        }
                    }
                }
            }
            else
            {
                if (goingToLast)
                {
                    if (PerformFadeIn())
                    {
                        finished = true;
                    }
                }
                else
                {
                    if (PerformFadeOut())
                    {
                        finished = true;
                    }
                }
            }

            blackImage.color = Color.Lerp(firstColor, lastColor, currentValue);
            if (finished)
            {
                if (!loop)
                {
                    performEffect = false;
                    if (disableWhenFinish)
                    {
                        Invoke("Disable", disableDelay);
                    }
                }
                else
                {
                    Invoke("StartEffect", initialDelay);
                }
            }
        }
    }

    private bool PerformFadeIn()
    {
        if (currentValue != 1f)
        {
            currentValue += speed * Time.unscaledDeltaTime;
            if (currentValue > 1f)
            {
                currentValue = 1f;
                return true;
            }
        }

        return false;
    }

    private bool PerformFadeOut()
    {
        if (currentValue != 0f)
        {
            currentValue -= speed * Time.unscaledDeltaTime;
            if (currentValue < 0f)
            {
                currentValue = 0f;
                return true;
            }
        }

        return false;
    }

    private void Disable()
    {
        if (deactiveParent)
        {
            transform.parent.gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        if (reset)
        {
            blackImage.color = Color.Lerp(firstColor, lastColor, 255);
        }
    }

    private void HalfCycleDelay()
    {
        halfCycle = true;
        goingToLast = !goingToLast;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True once the effect has run to completion (finished and no longer running).</summary>
    public bool HasFinished() => finished && !performEffect;

    public bool IsPingPongInHalfCycle() => halfCycle;

    /// <summary>Starts the effect using the firstToLast direction configured in the Inspector.</summary>
    public void StartEffect()
    {
        performEffect = true;
        finished      = false;
        halfCycle     = false;
        goingToLast   = firstToLast;
        currentValue  = goingToLast ? 0f : 1f;
    }

    /// <summary>
    /// Fades IN: transparent → opaque (firstColor → lastColor, currentValue 0 → 1).
    /// The black overlay becomes fully visible.
    /// Configure: firstColor = clear, lastColor = black (alpha 1).
    /// </summary>
    public void StartFadeIn()
    {
        gameObject.SetActive(true);
        performEffect = true;
        finished      = false;
        halfCycle     = false;
        goingToLast   = true;   // 0 → 1 — towards lastColor (opaque black)
        currentValue  = 0f;
        speed         = 1f / timeEffect;
    }

    /// <summary>
    /// Fades OUT: opaque → transparent (lastColor → firstColor, currentValue 1 → 0).
    /// The black overlay disappears revealing the scene.
    /// </summary>
    public void StartFadeOut()
    {
        gameObject.SetActive(true);
        performEffect = true;
        finished      = false;
        halfCycle     = false;
        goingToLast   = false;  // 1 → 0 — towards firstColor (transparent)
        currentValue  = 1f;
        speed         = 1f / timeEffect;
    }
}