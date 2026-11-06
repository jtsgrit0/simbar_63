using UnityEngine;

/// <summary>Hands-free walking and paired greetings on each original FBX skeleton.</summary>
public sealed partial class SimBarCharacterVisual
{
    private bool offStagePresentation;
    public void SetOffStagePresentation(bool value)
    {
        offStagePresentation=value;
        if(microphone!=null)microphone.gameObject.SetActive(!value);
        if(leftStick!=null)leftStick.gameObject.SetActive(!value);
        if(rightStick!=null)rightStick.gameObject.SetActive(!value);
        if(guitarPerformance!=null&&guitarPerformance.Guitar!=null)guitarPerformance.Guitar.gameObject.SetActive(!value);
    }
    public void ApplyOffStagePresentation(float timeSeconds,float walkingSpeed,string greeting=null,float progress=0f)
    {
        SetOffStagePresentation(true);
        string originalRole=role;
        try
        {
            // Use the authored relaxed gait rather than continuing to strum while
            // walking or carrying out a greeting. All model materials stay intact.
            role="guest";SetCrowdPerformance(SimBarCrowdAction.None,0f);
            ApplyPresentation(timeSeconds,.3f,walkingSpeed,0f);
        }
        finally{role=originalRole;}
        if(greeting!="highfive"&&greeting!="hug")return;
        progress=Mathf.Clamp01(progress);
        float reach=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0f,.3f,progress))
            *(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.72f,1f,progress)));
        if(greeting=="highfive")
        {
            SolvePresentationArm("r",Vector3.Lerp(new Vector3(.22f,.88f,.1f),new Vector3(.035f,1.78f,.42f),reach),new Vector3(.48f,1.35f,.18f),1f);
            RotatePresentationBone("torso",reach*2f,0f,0f);RotatePresentationBone("head",-reach*3f,0f,0f);
        }
        else
        {
            Vector3 left=Vector3.Lerp(new Vector3(-.22f,.9f,.1f),new Vector3(-.26f,1.32f,.48f),reach);
            Vector3 right=Vector3.Lerp(new Vector3(.22f,.9f,.1f),new Vector3(.26f,1.32f,.48f),reach);
            SolvePresentationArm("l",left,new Vector3(-.55f,1.2f,.27f),1f);
            SolvePresentationArm("r",right,new Vector3(.55f,1.2f,.27f),1f);
            RotatePresentationBone("torso",reach*5f,0f,0f);RotatePresentationBone("head",reach*3f,0f,0f);
        }
    }
}
