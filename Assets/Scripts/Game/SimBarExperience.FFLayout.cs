using System.Collections.Generic;
using UnityEngine;

/// <summary>Each renovated room keeps its own aisles, staff and standing guests.</summary>
public sealed partial class SimBarExperience
{
    private sealed class FFOriginalActor
    {
        public Vector3 home;
        public MotionKind kind;
        public float workMin,workMax;
    }
    private readonly Dictionary<MotionActor,FFOriginalActor> ffOriginalActors=new Dictionary<MotionActor,FFOriginalActor>();
    private int ffAppliedOwner=-1;
    private static readonly Vector3[] FFStandingHomes={
        new Vector3(-4.25f,0f,-5.25f),new Vector3(-3.3f,0f,-4.7f),new Vector3(-1.2f,0f,-3.9f),new Vector3(-.3f,0f,-4.7f),
        new Vector3(-5f,0f,1.7f),new Vector3(-3.4f,0f,2.5f),new Vector3(-1.8f,0f,1.25f),new Vector3(-.2f,0f,1.5f),
        new Vector3(-5.4f,0f,.25f),new Vector3(-1.9f,0f,-.15f),new Vector3(-1f,0f,.5f)
    };
    private static readonly Vector3[] FFOrderSlots={
        new Vector3(-6.1f,0f,-1.65f),new Vector3(-6.1f,0f,-2.45f),new Vector3(-5.7f,0f,-3.2f),
        new Vector3(-4.9f,0f,-3.6f),new Vector3(-4.1f,0f,-3.8f),new Vector3(-3.3f,0f,-3.8f),new Vector3(-2.5f,0f,-3.8f)
    };
    private static readonly Vector3[] FreebirdStandingHomes={
        new Vector3(-4.8f,0f,-4.8f),new Vector3(-3.3f,0f,-4.7f),new Vector3(-1.8f,0f,-4.7f),new Vector3(-.3f,0f,-4.7f),
        new Vector3(-7f,0f,1f),new Vector3(-7.2f,0f,2.5f),new Vector3(-2.3f,0f,.8f),new Vector3(-.3f,0f,.7f),
        new Vector3(-6f,0f,-1f),new Vector3(-4.2f,0f,-.8f),new Vector3(-2.6f,0f,-.9f)
    };
    private static readonly Vector3[] FreebirdOrderSlots={
        new Vector3(-4.8f,0f,1.75f),new Vector3(-3.95f,0f,1.75f),new Vector3(-3.1f,0f,1.75f),
        new Vector3(-2.25f,0f,1.75f),new Vector3(-1.4f,0f,1.45f),new Vector3(-.55f,0f,.65f),new Vector3(.3f,0f,-.15f)
    };
    private bool IsFFLayout=>selectedOwner==1;
    private bool IsFreebirdLayout=>selectedOwner==0;
    private bool IsStandingVenueLayout=>IsFFLayout||IsFreebirdLayout;
    private static int FFStandingIndex(string name)
    {
        if(name.StartsWith("Bar Guest ")&&int.TryParse(name.Substring(10),out int bar))return bar;
        if(name.StartsWith("Lounge Guest ")&&int.TryParse(name.Substring(13),out int lounge))return 4+lounge;
        return -1;
    }
    private void CaptureFFOriginalActor(MotionActor actor)
    {
        if(ffOriginalActors.ContainsKey(actor))return;
        ffOriginalActors[actor]=new FFOriginalActor{home=actor.home,kind=actor.kind,workMin=actor.workMin,workMax=actor.workMax};
        if(motionReady&&IsStandingVenueLayout)ApplyFFActorHome(actor,selectedOwner);
    }
    private void ApplyFFActorHome(MotionActor actor,int owner)
    {
        if(actor.person.root==null||!ffOriginalActors.TryGetValue(actor,out FFOriginalActor original))return;
        string name=actor.person.name;Vector3 home=original.home;
        actor.kind=original.kind;actor.workMin=original.workMin;actor.workMax=original.workMax;
        int standing=FFStandingIndex(name);
        bool changed=standing>=0||name=="Bartender"||name=="Server"||name.StartsWith("Hired ");
        if(!changed)return;
        if(owner==1)
        {
            if(standing>=0&&standing<FFStandingHomes.Length){home=FFStandingHomes[standing];actor.kind=MotionKind.Guest;}
            else if(name=="Bartender"){home=new Vector3(-8.9f,0f,-.4f);actor.workMin=-1.2f;actor.workMax=.5f;}
            else if(name=="Server")home=new Vector3(-6.1f,0f,-.5f);
            else if(name.StartsWith("Hired ")&&original.kind==MotionKind.BarStaff)home=new Vector3(-8.9f,0f,1.1f);
            else if(name=="Hired "+OpStaffNames[1])home=new Vector3(.5f,0f,-4.9f);
        }
        else if(owner==0)
        {
            if(standing>=0&&standing<FreebirdStandingHomes.Length){home=FreebirdStandingHomes[standing];actor.kind=MotionKind.Guest;}
            else if(name=="Bartender"){home=new Vector3(-4.8f,0f,4.85f);actor.workMin=-5.3f;actor.workMax=-4.05f;}
            else if(name=="Server")home=new Vector3(-1f,0f,2.7f);
            else if(name.StartsWith("Hired ")&&original.kind==MotionKind.BarStaff)home=new Vector3(-3.1f,0f,4.85f);
        }
        actor.home=home;actor.target=home;actor.person.root.position=worldRoot.TransformPoint(home);
        actor.path.Clear();actor.pathIndex=0;actor.queueIndex=-1;actor.routeFailed=false;actor.moving=false;actor.speed=0f;actor.stuck=0f;
        actor.state=MotionState.Idle;actor.wait=2f+actor.index%8*.8f;
    }
    private void RefreshFFVenueLayout()
    {
        if(!motionReady||ffAppliedOwner==selectedOwner)return;
        bool previousCustom=ffAppliedOwner==0||ffAppliedOwner==1,custom=IsStandingVenueLayout;ffAppliedOwner=selectedOwner;
        if(!previousCustom&&!custom)return;
        motionNavigation=new SimBarVenueNavigation(false,IsFFLayout,IsFreebirdLayout);floorNavigation=null;
        foreach(MotionActor actor in motionActors)
        {
            CaptureFFOriginalActor(actor);ApplyFFActorHome(actor,selectedOwner);
            if(actor.kind==MotionKind.Guest&&!motionNavigation.IsWalkable(MotionPosition(actor))&&motionNavigation.IsWalkable(actor.home))
                actor.person.root.position=worldRoot.TransformPoint(actor.home);
            actor.path.Clear();actor.pathIndex=0;actor.routeFailed=false;actor.moving=false;actor.queueIndex=-1;
            if(!AfterShowOwns(actor))actor.state=MotionState.Idle;
        }
        motionOrdersDirty=true;
        afterShowRoutes.Clear();afterShowApproaches.Clear();
        if(journey.afterShow!=null)foreach(AfterShowAudience fan in journey.afterShow.audience)
        {
            MotionActor actor=AfterShowActor(fan.id);
            if(actor==null||FFStandingIndex(actor.person.name)<0)continue;
            fan.homeX=actor.home.x;fan.homeZ=actor.home.z;
            if(fan.phase!="gone"){fan.x=actor.home.x;fan.y=actor.home.y;fan.z=actor.home.z;}
        }
        for(int i=0;i<floorCleaningCrew.Count;i++)
        {
            FloorWorkerActor worker=floorCleaningCrew[i];worker.savedPosition=worker.actor.home;
            worker.hasCleanupGoal=worker.hasParkGoal=false;
            if(worker!=floorEscortWorker&&FloorState!=null&&FloorState.cleanupActive)PrepareFloorWorkerRoute(worker,i);
        }
        if(floorEscortWorker!=null)
        {
            floorEscortWorker.savedPosition=floorEscortWorker.actor.home;
            if(floorEscortReturning)BeginFloorEscortReturn();
            else BuildFloorRoute(floorEscortRoute,floorEscortWorker.actor.person.root,FloorEscortMeetingPoint());
        }
    }
    private Vector3 VenueOrderSlot(int index)
    {
        Vector3[] slots=IsFFLayout?FFOrderSlots:IsFreebirdLayout?FreebirdOrderSlots:MotionOrderSlots;
        return slots[Mathf.Min(index,slots.Length-1)];
    }
    private Vector3 FFBarTarget(MotionActor actor)
    {
        if(actor.person.name!="Bartender")return actor.home;
        float cycle=Mathf.Repeat(motionClock+actor.index*1.9f,9f);
        if(IsFreebirdLayout)
        {
            float x=cycle<4.5f?actor.workMin+.15f:actor.workMax-.15f;
            if(IsPourInProgress)x=Mathf.Lerp(actor.workMin+.1f,actor.workMax-.1f,pourDrinkIndex/2f);
            return new Vector3(x,actor.home.y,4.85f);
        }
        float z=cycle<4.5f?actor.workMin+.15f:actor.workMax-.15f;
        if(IsPourInProgress)z=Mathf.Lerp(actor.workMin+.1f,actor.workMax-.1f,pourDrinkIndex/2f);
        return new Vector3(-8.9f,actor.home.y,z);
    }
}
