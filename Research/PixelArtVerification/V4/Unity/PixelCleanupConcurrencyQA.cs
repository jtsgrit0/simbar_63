using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class PixelCleanupConcurrencyQA
{
    const BindingFlags B=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Dir="Research/PixelArtVerification/V4/Unity/";
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static void Set(object target,string key,object value)=>target.GetType().GetField(key,B).SetValue(target,value);
    static object Call(object target,string key,params object[] args)
    {try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static object Property(object target,string key)=>target.GetType().GetProperty(key,B).GetValue(target);
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static Vector3 Position(object game,object actor)=>(Vector3)Call(game,"MotionPosition",actor);
    static Transform Root(object actor)=>(Transform)Get(Get(actor,"person"),"root");
    static void Prepare()
    {
        if(!Application.isPlaying||Game()==null||((IList)Get(Game(),"motionActors")).Count==0)throw new Exception("QA requires initialized Play Mode");
        var game=Game();Call(game,"ClearFloorActors");Call(game,"ResetAfterShow");var journey=Get(game,"journey");
        Set(journey,"session",Math.Max(1,(int)Get(journey,"session")+1));Set(journey,"inProgress",true);Set(journey,"secondsLeft",0f);Set(journey,"settlementPending",false);Set(journey,"guidePending",false);
        var floor=SimBarFloorMissions.NewState();floor.confettiThrows=1;floor.litter=24;Set(journey,"floorMissions",floor);
        Set(game,"journeyPhase",Enum.Parse(Get(game,"journeyPhase").GetType(),"Playing"));Set(game,"paused",false);Call(game,"FinishJourneyNight");
        var state=Get(journey,"afterShow");var world=(Transform)Get(game,"worldRoot");
        foreach(var member in (IList)Get(state,"members")){Set(member,"phase","gone");Set(member,"visible",false);Root(Call(game,"AfterShowActor",(string)Get(member,"id"))).gameObject.SetActive(false);}
        foreach(var fan in (IList)Get(state,"audience"))
        {
            Set(fan,"phase","done");Set(fan,"partner","");
            var actor=Call(game,"AfterShowActor",(string)Get(fan,"id"));Vector3 p=Position(game,actor);
            if((string)Get(fan,"id")=="patron-8"){p=new Vector3(2.2f,0f,-3.9f);Root(actor).position=world.TransformPoint(p);}
            Set(fan,"homeX",p.x);Set(fan,"homeZ",p.z);Set(fan,"x",p.x);Set(fan,"z",p.z);
        }
        Set(state,"completed",true);Call(game,"BeginFloorWorkerCleanup");Set(game,"paused",true);
    }
    public static object BlockedStation()
    {
        Prepare();var game=Game();var crew=(IList)Get(game,"floorCleaningCrew");
        Vector3 goal=(Vector3)Call(game,"FloorWorkerGoal",0),again=(Vector3)Call(game,"FloorWorkerGoal",0);var first=Get(crew[0],"actor");Vector3 before=Position(game,first);
        Set(game,"paused",false);int frames=0;for(;frames<800;frames++){Call(game,"TickFloorActors",.1f);if((int)Property(game,"CleanupReadyWorkers")>0)break;}Set(game,"paused",true);
        var result=new{goal=new{x=goal.x,z=goal.z},heldHomeClearance=Vector3.Distance(goal,new Vector3(2.2f,0f,-3.9f)),cachedStable=goal==again,readyWorkers=(int)Property(game,"CleanupReadyWorkers"),frames,moved=Vector3.Distance(before,Position(game,first))>.1f,qteBudgetUnchanged=((SimBarFloorMissions.State)Property(game,"FloorState")).reaction.cleanupWorkBudget==0f,cleaned=((SimBarFloorMissions.State)Property(game,"FloorState")).cleaned};
        File.WriteAllText(Dir+"cleanup-blocked-station.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object MidRouteCompletion()
    {
        Prepare();var game=Game();var journey=Get(game,"journey");var floor=(SimBarFloorMissions.State)Property(game,"FloorState");var crew=(IList)Get(game,"floorCleaningCrew");
        Set(game,"paused",false);for(int i=0;i<45;i++)Call(game,"TickFloorActors",.1f);
        var before=new Vector3[crew.Count];for(int i=0;i<crew.Count;i++)before[i]=Position(game,Get(crew[i],"actor"));
        floor.cleaned=floor.litter;floor.cleanupComplete=true;floor.cleanupActive=false;Call(game,"FinishJourneyNight");
        bool heldForPhysicalReturn=Get(game,"journeyPhase").ToString()=="FloorClosing"&&(bool)Property(game,"FloorStaffReturnPending");
        float maximumStep=0f;int frames=0;
        for(;frames<1200;frames++)
        {
            var starts=new Vector3[crew.Count];for(int i=0;i<crew.Count;i++)starts[i]=Position(game,Get(crew[i],"actor"));
            Call(game,"TickVenueMotion",.1f);Call(game,"TickAfterShow",.1f);Call(game,"TickFloorMissions",.1f);
            for(int i=0;i<crew.Count;i++)maximumStep=Mathf.Max(maximumStep,Vector3.Distance(starts[i],Position(game,Get(crew[i],"actor"))));
            if(Get(game,"journeyPhase").ToString()=="Results")break;
        }
        Set(game,"paused",true);var workers=new JArray();bool allReturned=true,physicallyMoved=true;
        for(int i=0;i<crew.Count;i++)
        {
            var actor=Get(crew[i],"actor");Vector3 p=Position(game,actor),park=(Vector3)Call(game,"FloorWorkerParkGoal",i);float remaining=Vector3.Distance(p,park);allReturned&=remaining<=.051f;physicallyMoved&=Vector3.Distance(before[i],p)>.1f;
            workers.Add(new JObject{["name"]=(string)Get(Get(actor,"person"),"name"),["x"]=p.x,["z"]=p.z,["remaining"]=remaining,["behindBar"]=p.x< -1.65f});
        }
        var result=new{heldForPhysicalReturn,allReturned,physicallyMoved,maximumStep,noTeleports=maximumStep<=.156f,frames,phase=Get(game,"journeyPhase").ToString(),settled=(int)Get(journey,"session")== (int)Get(journey,"settledSession"),workers};
        File.WriteAllText(Dir+"cleanup-midroute-completion.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object HiredParkReservation()
    {
        Prepare();var game=Game();object hired=null;bool created=false;foreach(var actor in (IList)Get(game,"motionActors"))if(Get(actor,"kind").ToString()=="FloorStaff"){hired=actor;break;}
        if(hired==null)
        {
            var person=Call(game,"CreatePerson","Hired QA",new Vector3(-.4f,0f,-4.9f),Color.cyan,Color.black,false,(Transform)Get(game,"worldRoot"),"guest",0f);
            hired=((IDictionary)Get(game,"motionLookup"))[person];
            created=true;
        }
        Vector3 previousHome=(Vector3)Get(hired,"home");bool previousActive=Root(hired).gameObject.activeSelf;
        Vector3 heldHome=new Vector3(2.2f,0f,-3.9f);Set(hired,"home",heldHome);Root(hired).gameObject.SetActive(true);Call(game,"AddFloorWorker",hired,-1);
        var crew=(IList)Get(game,"floorCleaningCrew");int index=crew.Count-1;Vector3 goal=(Vector3)Call(game,"FloorWorkerParkGoal",index),again=(Vector3)Call(game,"FloorWorkerParkGoal",index);
        var result=new{hiredName=(string)Get(Get(hired,"person"),"name"),goal=new{x=goal.x,z=goal.z},heldHomeClearance=Vector3.Distance(goal,heldHome),cachedStable=goal==again,clear=Vector3.Distance(goal,heldHome)>=.449f};
        Set(hired,"home",previousHome);Root(hired).gameObject.SetActive(previousActive&&!created);
        File.WriteAllText(Dir+"cleanup-hired-park.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
