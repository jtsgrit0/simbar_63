using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stage : MonoBehaviour
{
    public Transform[] performers;
    public float performanceDuration = 180f;
    public float cooldownDuration = 60f;
    public AudioClip bgmClip;

    private float timer;
    private bool isPerforming;

    private void Update()
    {
        if (!isPerforming)
        {
            timer += Time.deltaTime;
            if (timer >= cooldownDuration)
            {
                StartPerformance();
            }
        }
        else
        {
            timer += Time.deltaTime;
            if (timer >= performanceDuration + cooldownDuration)
            {
                StopPerformance();
                timer = 0f;
            }
        }
    }

    private void StartPerformance()
    {
        isPerforming = true;
        timer = 0f;

        foreach (var performer in performers)
        {
            if (performer != null)
            {
                Animator anim = performer.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetTrigger("Play");
                }
            }
        }

        if (bgmClip != null)
        {
            AudioManager.Instance.PlayBGM(bgmClip);
        }
    }

    private void StopPerformance()
    {
        isPerforming = false;
        foreach (var performer in performers)
        {
            if (performer != null)
            {
                Animator anim = performer.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetTrigger("Stop");
                }
            }
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM();
        }
    }
}
