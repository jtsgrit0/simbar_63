using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PaperFinaleQA
{
    const string Dir="Research/PixelArtVerification/PaperFinale/Unity/",ProgressPath="Library/SimBarQA/PaperFinale-progress.json";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static void Set(object target,string key,object value)=>target.GetType().GetField(key,B).SetValue(target,value);
    static object Call(object target,string key,params object[] args){try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();return null;}}
    static SimBarExperience pinnedGame;
    static SimBarExperience Game()
    {
        if(pinnedGame!=null)return pinnedGame;var games=UnityEngine.Object.FindObjectsByType<SimBarExperience>();
        Require(games.Length==1,"Expected exactly one runtime controller, found "+games.Length);return pinnedGame=games[0];
    }
    static object Journey()=>Get(Game(),"journey");
    static SimBarFloorMissions.State Floor()=>(SimBarFloorMissions.State)Get(Journey(),"floorMissions");
    static bool Prop(string key)=>(bool)typeof(SimBarExperience).GetProperty(key,B).GetValue(Game());
    static int Ready()=>(int)typeof(SimBarExperience).GetProperty("CleanupReadyWorkers",B).GetValue(Game());
    static IEnumerable<object> Particles()=>((IEnumerable)Get(Game(),"pixelParticles")).Cast<object>().Where(p=>((Graphic)Get(p,"image")).gameObject.activeSelf);
    static IEnumerable<object> Members()=>((IEnumerable)Get(Get(Journey(),"afterShow"),"members")).Cast<object>();
    static IEnumerable<Graphic> Ground()=>((IEnumerable)Get(Game(),"pixelPaper")).Cast<Graphic>().Where(p=>p.gameObject.activeSelf);
    static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    static object Record(string name,object value,bool capture=false){Directory.CreateDirectory(Dir);Call(Game(),"LateUpdate");Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"),2);return value;}
    static void Refresh(){Call(Game(),"RefreshJourneyHUD");Call(Game(),"RefreshFloorMissionUI");Call(Game(),"LateUpdate");Canvas.ForceUpdateCanvases();}
    static void Step()
    {
        foreach(string method in new[]{"TickVenueMotion","TickAfterShow","TickFloorMissions","UpdateAirbornePixelPaper"})
            try{Call(Game(),method,.1f);}
            catch(Exception error){Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"controller-exception.json",JsonConvert.SerializeObject(new{method,exception=error.ToString(),controllers=UnityEngine.Object.FindObjectsByType<SimBarExperience>().Length,afterShowElapsed=Get(Get(Journey(),"afterShow"),"elapsed"),members=MemberStatus(),crew=CrewStatus(),litter=Floor().litter,cleaned=Floor().cleaned},Formatting.Indented));throw;}
    }
    static string PaperSignature()=>string.Join("|",Particles().Select(p=>((Vector3)Get(p,"position")).ToString("F6")+":"+((float)Get(p,"age")).ToString("F6")));
    static object[] MemberStatus()=>Members().Select(m=>new{id=Get(m,"id"),phase=Get(m,"phase"),greeting=Get(m,"greeting")}).Cast<object>().ToArray();
    static object[] CrewStatus()=>((IEnumerable)Get(Game(),"floorCleaningCrew")).Cast<object>().Select(w=>{var a=Get(w,"actor");var p=(Vector3)Call(Game(),"MotionPosition",a);return new{name=Get(Get(a,"person"),"name"),position=p.ToString("F3"),ready=Get(Get(w,"route"),"ready"),action=Call(Game(),"PixelActionFor",a)};}).Cast<object>().ToArray();
    static void NoPopupMusic(){Require(!Resources.FindObjectsOfTypeAll<Button>().Any(b=>b.name.StartsWith("Farewell Music")||b.name.StartsWith("Results Music")),"A closure popup retained a music control");}
    public static object Rules(){var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("SimBarRuntimeVerification")).First(t=>t!=null);return JObject.Parse((string)type.GetMethod("CheckFloorMissions",B).Invoke(null,null));}
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();File.WriteAllText("Library/SimBarQA/PaperFinale-GameView.json",JsonConvert.SerializeObject(new{index=type.GetProperty("selectedSizeIndex",B).GetValue(window)}));type.GetProperty("selectedSizeIndex",B).SetValue(window,1);return new{saved=true};
    }
    public static object BeginClosing()
    {
        var game=Game();Set(Journey(),"inProgress",false);Set(Journey(),"tutorialComplete",true);Call(game,"StartFreshJourneyNight",false);game.enabled=false;Set(game,"paused",false);SimBarLocalization.SetLanguage(true);Set(game,"cash",1000f);Set(game,"fans",100);Set(game,"performance",.3f);
        SimBarFloorChallenges.State schedule=null;int seed=0;for(int i=1;i<1000;i++){var s=SimBarFloorChallenges.NewState(i);if(s.paperCount==0&&s.guestScheduled){seed=i;schedule=s;break;}}Require(schedule!=null,"No naturally quiet paper schedule found");
        Floor().reaction=schedule;Set(Journey(),"secondsLeft",180f-schedule.guestAt-.1f);Call(game,"TickFloorMissions",.1f);Require(Floor().litter==0&&Floor().guestActive&&schedule.activeKind==SimBarFloorChallenges.Guest,"Quiet schedule/guest due event did not apply");
        Call(game,"FloorQuickPress",SimBarFloorChallenges.Guest);int presses=schedule.presses;float seconds=schedule.secondsLeft;Require(presses==1,"Partial F input missing");Set(Journey(),"secondsLeft",0f);Call(game,"FinishJourneyNight");Refresh();
        Require(Floor().finaleEnsured&&Floor().confettiThrows==1&&Floor().tissueThrows==1&&Floor().litter==56&&Floor().cleanupActive,"Missing finale did not create both effects and cleanup debt");
        Require(schedule.activeKind==SimBarFloorChallenges.Guest&&schedule.presses==presses&&schedule.secondsLeft==seconds,"Closing canceled the existing partial F prompt");
        Require((int)Get(game,"fans")==104&&Mathf.Abs((float)Get(game,"performance")-.38f)<.0001f,"Finale normal audience rewards differ");
        Require(Particles().Count(p=>!(bool)Get(p,"tissue"))==56&&Particles().Count(p=>(bool)Get(p,"tissue"))==28&&Ground().Count()==40,"Visible paper counts differ from final contract");
        Require(!Prop("StagePerformanceLightingActive")&&(bool)Get(game,"musicPaused")&&!(bool)Get(game,"paused"),"Closing lights/music-only pause changed");NoPopupMusic();
        File.WriteAllText(ProgressPath,new JObject{["ticks"]=0,["seenC"]=false,["concurrent"]=false,["captured"]=false,["farewellCaptured"]=false}.ToString());
        return Record("closing-both-paper-effects",new{naturalQuietScheduleSeed=seed,midshowPaperCount=0,confetti=56,tissueRibbons=28,groundConfetti=24,groundTissue=16,litter=56,normalFanGain=4,normalEnergyGain=.08,activeFPartialPreserved=true,cleanupAssigned=true,stageOff=true,musicOnlyAutoPaused=true,popupMusicControls=0},true);
    }
    public static object PauseAndReplay()
    {
        var game=Game();Set(game,"paused",true);string before=PaperSignature();float cleaned=Floor().cleaned;for(int i=0;i<8;i++){Call(game,"TickFloorMissions",.1f);Call(game,"LateUpdate");}Require(before==PaperSignature()&&Floor().cleaned==cleaned,"Paused paper or cleanup moved");
        Set(game,"paused",false);int fans=(int)Get(game,"fans"),count=Particles().Count(),cf=Floor().confettiThrows,tf=Floor().tissueThrows;Call(game,"TryBeginFloorClosing");Refresh();Require((int)Get(game,"fans")==fans&&Floor().confettiThrows==cf&&Floor().tissueThrows==tf&&Particles().Count()==count,"Repeated closing reburst or rewarded paper");
        return Record("pause-and-repeat",new{pauseFrozen=true,repeatClosingNoBurst=true,repeatClosingNoReward=true,particleCount=count});
    }
    public static object ConcurrentChunk()
    {
        var game=Game();var progress=JObject.Parse(File.ReadAllText(ProgressPath));bool captured=(bool)progress["captured"];
        for(int i=0;i<100;i++)
        {
            if(Prop("AfterShowFarewellPending"))break;Step();progress["ticks"]=(int)progress["ticks"]+1;
            var reaction=Floor().reaction;bool c=reaction.activeKind==SimBarFloorChallenges.Cleanup;if(c)progress["seenC"]=true;
            if(c)Call(game,"FloorQuickPress",SimBarFloorChallenges.Cleanup);
            bool greeting=Members().Any(m=>new[]{"approach","highfive","hug"}.Contains((string)Get(m,"phase")));
            bool social=Members().Any(m=>new[]{"highfive","hug"}.Contains((string)Get(m,"phase")));
            if(c&&greeting&&(bool?)progress["cCaptured"]!=true){Refresh();Record("cleanup-c-prompt-during-greeting",new{elapsed=(int)progress["ticks"]*.1f,actualCPrompt=true,readyWorkers=Ready(),members=MemberStatus(),crew=CrewStatus()},true);progress["cCaptured"]=true;}
            if(Floor().cleaned>0f&&greeting&&Ready()>0)progress["concurrent"]=true;
            if(!captured&&Floor().cleaned>0f&&social&&Ready()>0)
            {
                Refresh();Record("concurrent-cleanup-greeting",new{elapsed=(int)progress["ticks"]*.1f,cleaned=Floor().cleaned,litter=Floor().litter,remaining=SimBarFloorMissions.Remaining(Floor()),readyWorkers=Ready(),cleanupCPromptSeen=(bool)progress["seenC"],currentPrompt=Floor().reaction.activeKind,groundMarks=Ground().Count(),airborneMarks=Particles().Count(),members=MemberStatus(),crew=CrewStatus(),guestStillActive=Floor().guestActive},true);progress["captured"]=true;break;
            }
        }
        File.WriteAllText(ProgressPath,progress.ToString());Refresh();return new{ticks=(int)progress["ticks"],seenC=(bool)progress["seenC"],concurrent=(bool)progress["concurrent"],captured=(bool)progress["captured"],cleaned=Floor().cleaned,ready=Ready(),members=MemberStatus()};
    }
    public static object SaveResume()
    {
        var game=Game();Require(Floor().cleaned>0f&&!Floor().cleanupComplete,"Need actual partial cleanup for save regression");int fans=(int)Get(game,"fans");float cleaned=Floor().cleaned,budget=Floor().reaction.cleanupWorkBudget;int cf=Floor().confettiThrows,tf=Floor().tissueThrows;
        string saved=(string)Call(game,"ExportJourneyState");Call(game,"ImportJourneyState",saved);Call(game,"ContinueJourney");game.enabled=false;Refresh();
        Require(Floor().finaleEnsured&&Floor().confettiThrows==cf&&Floor().tissueThrows==tf&&Mathf.Abs(Floor().cleaned-cleaned)<.0001f&&Mathf.Abs(Floor().reaction.cleanupWorkBudget-budget)<.0001f&&(int)Get(game,"fans")==fans,"Saved closing repeated paper/reward or changed earned work");
        Require(Particles().Count()==0&&Ground().Count()>0,"Saved debt incorrectly reburst or lost its ground paper");
        return Record("partial-cleanup-save-resume",new{finaleLatchPreserved=true,noAirReplay=true,groundDebtRestored=true,cleaned,budget,groundMarks=Ground().Count(),noDuplicateRewards=true},true);
    }
    public static object FinishChunk()
    {
        var game=Game();var progress=JObject.Parse(File.ReadAllText(ProgressPath));
        for(int i=0;i<150&&!Prop("JourneyIsResults");i++)
        {
            if(Prop("AfterShowFarewellPending"))
            {
                NoPopupMusic();Refresh();if(!(bool)progress["farewellCaptured"]){Record("farewell-without-music-controls",new{popupMusicControls=0,onlyAcknowledge=true,remainingPaper=SimBarFloorMissions.Remaining(Floor())},true);progress["farewellCaptured"]=true;File.WriteAllText(ProgressPath,progress.ToString());return new{results=false,farewellCaptured=true,cleaned=Floor().cleaned};}
                GameObject.Find("Journey Farewell Acknowledge").GetComponent<Button>().onClick.Invoke();
            }
            if(Floor().reaction.activeKind!=0)Call(game,"FloorQuickPress",Floor().reaction.activeKind);Step();
            if(Floor().cleaned>=Floor().litter*.5f&&!Floor().cleanupComplete&&(bool?)progress["halfCaptured"]!=true)
            {
                Refresh();float ratio=1f-Floor().cleaned/Floor().litter;int expected=Mathf.CeilToInt(Mathf.Min(64,Floor().confettiThrows*24)*ratio)+Mathf.CeilToInt(Mathf.Min(40,Floor().tissueThrows*16)*ratio);
                Require(Ground().Count()==expected&&expected<=20,"Visible ground paper did not follow actual earned cleanup work");
                Record("half-clean-ground-paper",new{cleaned=Floor().cleaned,litter=Floor().litter,initialGroundMarks=40,currentGroundMarks=Ground().Count(),expected,crew=CrewStatus()},true);progress["halfCaptured"]=true;File.WriteAllText(ProgressPath,progress.ToString());return new{results=false,halfCaptured=true,cleaned=Floor().cleaned};
            }
        }
        Refresh();bool done=Prop("JourneyIsResults");if(done)
        {
            NoPopupMusic();Require(Floor().cleanupComplete&&Floor().cleanupRewardClaimed&&SimBarFloorMissions.Remaining(Floor())==0&&Ground().Count()==0,"Results opened before actual cleanup and paper removal");
            Record("results-floor-clean-no-music-controls",new{cleanupComplete=true,cleanupReceiptClaimed=true,remainingPaper=0,groundMarks=0,popupMusicControls=0,finaleEnsured=Floor().finaleEnsured},true);
        }
        File.WriteAllText(ProgressPath,progress.ToString());return new{results=done,cleaned=Floor().cleaned,remaining=SimBarFloorMissions.Remaining(Floor()),guestActive=Floor().guestActive,afterShowPending=Prop("AfterShowPending"),phase=Get(game,"journeyPhase").ToString()};
    }
    public static object HalfCleanChunk()
    {
        for(int i=0;i<100&&Floor().cleaned<Floor().litter*.5f;i++){if(Floor().reaction.activeKind!=0)Call(Game(),"FloorQuickPress",Floor().reaction.activeKind);Step();}
        Refresh();bool half=Floor().cleaned>=Floor().litter*.5f;
        if(half){float ratio=1f-Floor().cleaned/Floor().litter;int expected=Mathf.CeilToInt(Mathf.Min(64,Floor().confettiThrows*24)*ratio)+Mathf.CeilToInt(Mathf.Min(40,Floor().tissueThrows*16)*ratio);Require(Ground().Count()==expected&&expected<=20,"Ground paper did not shrink with actual earned work");Record("half-clean-ground-paper",new{cleaned=Floor().cleaned,litter=Floor().litter,initialGroundMarks=40,currentGroundMarks=Ground().Count(),expected,airborneMarks=Particles().Count(),crew=CrewStatus()},true);}
        return new{half,cleaned=Floor().cleaned,groundMarks=Ground().Count(),airborneMarks=Particles().Count()};
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/PaperFinale-GameView.json"));type.GetProperty("selectedSizeIndex",B).SetValue(window,(int)viewport["index"]);return Readback();
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/PaperFinale-GameView.json"));
        var result=new{prefsRestored=exact,gameViewRestored=(int)type.GetProperty("selectedSizeIndex",B).GetValue(window)==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling};File.WriteAllText(Dir+"preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
