using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class PixelV4QA
{
    const BindingFlags B=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Dir="Research/PixelArtVerification/V4/Unity/";
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static void Set(object target,string key,object value)=>target.GetType().GetField(key,B).SetValue(target,value);
    static object Call(object target,string key,params object[] args)
    {try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new{exists=PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey),value=PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey)}));
        return new{saved=true};
    }
    public static object Restore()
    {
        var b=JObject.Parse(File.ReadAllText(Dir+"prefs-backup.json"));
        if((bool)b["exists"])PlayerPrefs.SetString(SimBarExperience.OperationsSaveKey,(string)b["value"]);else PlayerPrefs.DeleteKey(SimBarExperience.OperationsSaveKey);
        PlayerPrefs.Save();return new{restored=true};
    }
    public static object PreservationContract()
    {
        var backup=JObject.Parse(File.ReadAllText(Dir+"prefs-backup.json"));
        bool savedValueRestored=(bool)backup["exists"]==PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey)
            &&(string)backup["value"]==PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey);
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var result=new{savedValueRestored,scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying};
        File.WriteAllText(Dir+"preservation-check.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object Begin()
    {
        var game=Game();var journey=Get(game,"journey");
        Set(journey,"session",Math.Max(1,(int)Get(journey,"session")+1));Set(journey,"inProgress",true);Set(journey,"secondsLeft",0f);
        Set(journey,"afterShow",null);Set(journey,"settlementPending",false);Set(journey,"guidePending",false);Set(journey,"floorMissions",SimBarFloorMissions.NewState());
        Set(game,"journeyPhase",Enum.Parse(Get(game,"journeyPhase").GetType(),"Playing"));Set(game,"paused",false);
        Call(game,"FinishJourneyNight");Set(game,"paused",true);
        File.WriteAllText(Dir+"observed-phases.json","[]");
        return State();
    }
    public static object Performance(float seconds=1f)
    {
        var game=Game();Call(game,"ResetAfterShow");var journey=Get(game,"journey");
        Set(journey,"inProgress",true);Set(journey,"secondsLeft",180f);Set(journey,"floorClosing",false);Set(journey,"guidePending",false);
        Set(game,"journeyPhase",Enum.Parse(Get(game,"journeyPhase").GetType(),"Playing"));Set(game,"paused",true);Set(game,"pixelAnimationSeconds",seconds);
        Call(game,"RefreshJourneyHUD");Call(game,"LateUpdate");return new{ready=true,seconds};
    }
    public static object Until(string member,string phase,float minProgress=.35f)
    {
        var game=Game();Set(game,"paused",false);bool reached=false;int frames=0;
        var phases=new HashSet<string>();foreach(var value in JArray.Parse(File.ReadAllText(Dir+"observed-phases.json")))phases.Add((string)value);
        for(;frames<2000;frames++)
        {
            Call(game,"TickVenueMotion",.1f);Call(game,"TickAfterShow",.1f);Set(game,"pixelAnimationSeconds",(float)Get(game,"pixelAnimationSeconds")+.1f);
            var state=JObject.Parse(JsonUtility.ToJson(Get(Get(game,"journey"),"afterShow")));
            foreach(var value in state["members"])phases.Add((string)value["id"]+":"+(string)value["phase"]);
            foreach(var value in state["members"])if((string)value["id"]==member&&(string)value["phase"]==phase&&(float)value["timer"]>=minProgress)reached=true;
            if(reached)break;
        }
        File.WriteAllText(Dir+"observed-phases.json",JsonConvert.SerializeObject(phases,Formatting.Indented));
        Set(game,"paused",true);Call(game,"LateUpdate");return new{reached,frames,state=State()};
    }
    public static object HighFive()=>Until("singer","highfive",.6f);
    public static object Hug()=>Until("singer","hug",.8f);
    public static object PauseContract()
    {
        var game=Game();Set(game,"paused",true);Call(game,"LateUpdate");
        string before=JsonUtility.ToJson(Get(Get(game,"journey"),"afterShow"));float seconds=(float)Get(game,"pixelAnimationSeconds");
        var lights=(IList)Get(game,"pixelLightViews");var cone=(UnityEngine.UI.Graphic)((IList)Get(lights[0],"cones"))[0];var color=cone.color;
        for(int i=0;i<20;i++){Call(game,"TickAfterShow",.1f);Call(game,"LateUpdate");}
        var result=new{stateFrozen=before==JsonUtility.ToJson(Get(Get(game,"journey"),"afterShow")),clockFrozen=seconds==(float)Get(game,"pixelAnimationSeconds"),lightFrozen=color==cone.color,lightVisible=cone.gameObject.activeInHierarchy&&cone.color.a>0f};
        File.WriteAllText(Dir+"pause-check.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object VisualContract()
    {
        var game=Game();Performance();var actors=(IList)Get(game,"motionActors");object singer=null;
        foreach(var actor in actors){var patron=Get(actor,"person");if((string)Get(patron,"name")=="Singer")singer=actor;}
        var views=(IDictionary)Get(game,"pixelActorViews");var image=(UnityEngine.UI.RawImage)Get(views[singer],"image");var frames=new HashSet<string>();
        for(int i=0;i<32;i++){Set(game,"pixelAnimationSeconds",i/8f);Call(game,"LateUpdate");frames.Add(image.uvRect.ToString("F6"));}
        var before=image.uvRect;Call(game,"LateUpdate");var after=image.uvRect;
        var lights=(IList)Get(game,"pixelLightViews");var root=(Transform)Get(game,"pixelLightRoot");
        var result=new{performanceFrames=frames.Count,pauseFreezesFrame=before==after,atlas=image.texture.name,width=image.texture.width,height=image.texture.height,lightBanks=lights.Count,lightsVisible=root.gameObject.activeInHierarchy,conesPerBank=((IList)Get(lights[0],"cones")).Count,poolsPerBank=((IList)Get(lights[0],"pools")).Count};
        File.WriteAllText(Dir+"visual-contract.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object Advance(int frames=100)
    {
        var game=Game();var seen=JArray.Parse(File.ReadAllText(Dir+"observed-phases.json"));var phases=new HashSet<string>();foreach(var value in seen)phases.Add((string)value);
        Set(game,"paused",false);
        for(int i=0;i<frames;i++)
        {
            Call(game,"TickVenueMotion",.1f);Call(game,"TickAfterShow",.1f);
            var state=JObject.Parse(JsonUtility.ToJson(Get(Get(game,"journey"),"afterShow")));
            foreach(var member in state["members"])phases.Add((string)member["id"]+":"+(string)member["phase"]);
        }
        Set(game,"paused",true);File.WriteAllText(Dir+"observed-phases.json",JsonConvert.SerializeObject(phases,Formatting.Indented));
        return State();
    }
    public static object State()
    {
        var game=Game();var journey=Get(game,"journey");Call(game,"SyncAfterShow");
        string json=JsonUtility.ToJson(Get(journey,"afterShow"),true);File.WriteAllText(Dir+"aftershow-state.json",json);
        return JObject.Parse(json);
    }
    public static object Resume()
    {
        var game=Game();string before=(string)Call(game,"ExportJourneyState");var state=JObject.Parse(before)["afterShow"];
        Call(game,"ImportJourneyState",before);Call(game,"ContinueJourney");Set(game,"paused",true);
        string after=(string)Call(game,"ExportJourneyState");var next=JObject.Parse(after)["afterShow"];bool same=true;
        for(int i=0;i<4;i++)foreach(string field in new[]{"id","phase","waypoint","greeting","timer","x","z","stageLevel","visible"})same&=JToken.DeepEquals(state["members"][i][field],next["members"][i][field]);
        var result=new{samePositions=same,sameAudience=JToken.DeepEquals(state["audience"],next["audience"]),sameElapsed=JToken.DeepEquals(state["elapsed"],next["elapsed"]),sameReturningFan=JToken.DeepEquals(state["returningFan"],next["returningFan"]),returningFan=(string)state["returningFan"],bytes=before.Length};
        File.WriteAllText(Dir+"resume-check.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object Settle()
    {
        var game=Game();var journey=Get(game,"journey");Call(game,"FinishJourneyNight");int count=(int)Get(journey,"completedNights"),xp=(int)Get(journey,"xp");float cash=(float)Get(game,"cash");
        Call(game,"FinishJourneyNight");Call(game,"SettleJourneyNight");
        var result=new{settled=(int)Get(journey,"session")== (int)Get(journey,"settledSession"),onlyOnce=count==(int)Get(journey,"completedNights")&&xp==(int)Get(journey,"xp")&&cash==(float)Get(game,"cash"),count,xp,cash};
        File.WriteAllText(Dir+"settlement-check.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    static object Contact(string name)
    {
        var game=Game();Call(game,"LateUpdate");var actor=Call(game,"AfterShowActor","singer");var guest=Call(game,"AfterShowActor",name=="highfive"?"patron-8":"patron-9");
        var views=(IDictionary)Get(game,"pixelActorViews");var a=(RectTransform)Get(views[actor],"root");var b=(RectTransform)Get(views[guest],"root");
        var result=new{action=name,memberPixelX=a.anchoredPosition.x,memberPixelY=a.anchoredPosition.y,fanPixelX=b.anchoredPosition.x,fanPixelY=b.anchoredPosition.y,pixelDistance=Vector2.Distance(a.anchoredPosition,b.anchoredPosition),worldDistance=Vector3.Distance((Vector3)Call(game,"MotionPosition",actor),(Vector3)Call(game,"MotionPosition",guest)),progress=Get(actor,"afterShowProgress")};
        File.WriteAllText(Dir+name+"-contact.json",JsonConvert.SerializeObject(result,Formatting.Indented));ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"));return result;
    }
    public static object CaptureHighFive()=>Contact("highfive");
    public static object CaptureHug()=>Contact("hug");
    public static object CapturePerformance(){Performance(1.2f);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"performance.png"));return new{queued=true};}
    public static object QueueCheckpoint()
    {
        var game=Game();var phases=new HashSet<string>();foreach(var value in JArray.Parse(File.ReadAllText(Dir+"observed-phases.json")))phases.Add((string)value);
        Set(game,"paused",false);bool reached=false;int frames=0,returning=0,moving=0;string active="";
        for(;frames<2000;frames++)
        {
            Call(game,"TickVenueMotion",.1f);Call(game,"TickAfterShow",.1f);
            var state=JObject.Parse(JsonUtility.ToJson(Get(Get(game,"journey"),"afterShow")));returning=0;moving=0;active=(string)state["returningFan"];
            foreach(var member in state["members"])phases.Add((string)member["id"]+":"+(string)member["phase"]);
            foreach(var fan in state["audience"])if((string)fan["phase"]=="return"){returning++;if((bool)Get(Call(game,"AfterShowActor",(string)fan["id"]),"moving"))moving++;}
            if(returning>=2&&!string.IsNullOrEmpty(active)){reached=true;break;}
        }
        Set(game,"paused",true);Call(game,"LateUpdate");File.WriteAllText(Dir+"observed-phases.json",JsonConvert.SerializeObject(phases,Formatting.Indented));
        var result=new{reached,frames,returning,movingReturners=moving,returningFan=active,onePhysicalWalker=moving<=1};File.WriteAllText(Dir+"queue-checkpoint.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object Complete()
    {
        var game=Game();var journey=Get(game,"journey");int completedBefore=(int)Get(journey,"completedNights");
        Call(game,"FinishJourneyNight");bool pendingGate=Get(game,"journeyPhase").ToString()=="FloorClosing"&&(int)Get(journey,"completedNights")==completedBefore;
        var phases=new HashSet<string>();foreach(var value in JArray.Parse(File.ReadAllText(Dir+"observed-phases.json")))phases.Add((string)value);
        bool bandOnlyGate=false,allGone=false,allFans=false;int frames=0;JObject state=null;
        Set(game,"paused",false);
        for(;frames<6000;frames++)
        {
            Call(game,"TickVenueMotion",.1f);Call(game,"TickAfterShow",.1f);
            state=JObject.Parse(JsonUtility.ToJson(Get(journey,"afterShow")));
            allGone=true;allFans=true;
            foreach(var member in state["members"]){string phase=(string)member["phase"];phases.Add((string)member["id"]+":"+phase);allGone&=phase=="gone";}
            foreach(var fan in state["audience"])allFans&=(string)fan["phase"]=="done";
            if(allGone&&!allFans){Call(game,"FinishJourneyNight");bandOnlyGate|=Get(game,"journeyPhase").ToString()=="FloorClosing"&&(int)Get(journey,"completedNights")==completedBefore;}
            Call(game,"TickFloorMissions",.1f);
            if(Get(game,"journeyPhase").ToString()=="Results")break;
        }
        Set(game,"paused",true);Call(game,"LateUpdate");
        bool allFansAtHome=true;var homes=new JArray();foreach(var fan in state["audience"]){float dx=(float)fan["x"]-(float)fan["homeX"],dz=(float)fan["z"]-(float)fan["homeZ"];float distance=Mathf.Sqrt(dx*dx+dz*dz);allFansAtHome&=distance<=.021f;homes.Add(new JObject{["id"]=(string)fan["id"],["phase"]=(string)fan["phase"],["homeDistance"]=distance});}
        int count=(int)Get(journey,"completedNights"),xp=(int)Get(journey,"xp");float cash=(float)Get(game,"cash");Call(game,"FinishJourneyNight");Call(game,"SettleJourneyNight");
        var result=new{frames,seconds=frames*.1f,pendingGate,bandOnlyGate,allGone,allFans,allFansAtHome,completed=(bool)state["completed"],phase=Get(game,"journeyPhase").ToString(),settled=(int)Get(journey,"session")== (int)Get(journey,"settledSession"),onlyOnce=count==(int)Get(journey,"completedNights")&&xp==(int)Get(journey,"xp")&&cash==(float)Get(game,"cash"),count,homes};
        File.WriteAllText(Dir+"completion-check.json",JsonConvert.SerializeObject(result,Formatting.Indented));File.WriteAllText(Dir+"aftershow-state.json",state.ToString(Formatting.Indented));File.WriteAllText(Dir+"observed-phases.json",JsonConvert.SerializeObject(phases,Formatting.Indented));return result;
    }
}
