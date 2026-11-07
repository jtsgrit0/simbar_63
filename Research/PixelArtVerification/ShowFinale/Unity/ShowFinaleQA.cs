using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class ShowFinaleQA
{
    const string Dir="Research/PixelArtVerification/ShowFinale/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static void Set(object target,string key,object value)=>target.GetType().GetField(key,B).SetValue(target,value);
    static object Call(object target,string key,params object[] args){try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static object Journey()=>Get(Game(),"journey");
    static object State()=>Get(Journey(),"afterShow");
    static IList Members()=>(IList)Get(State(),"members");
    static IList Audience()=>(IList)Get(State(),"audience");
    static object Actor(string id)=>Call(Game(),"AfterShowActor",id);
    static Transform Root(object actor)=>(Transform)Get(Get(actor,"person"),"root");
    static Vector3 Position(object actor)=>(Vector3)Call(Game(),"MotionPosition",actor);
    static object View(object actor)=>((IDictionary)Get(Game(),"pixelActorViews"))[actor];
    static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    static object Record(string name,object value,bool capture=false){Directory.CreateDirectory(Dir);Call(Game(),"LateUpdate");Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"),2);return value;}
    static void Step(float delta=.1f){var game=Game();Call(game,"TickVenueMotion",delta);Call(game,"TickAfterShow",delta);Call(game,"TickFloorMissions",delta);}
    static IEnumerable<object> Items()=>Members().Cast<object>().Concat(Audience().Cast<object>());
    static string[] Ids()=>Items().Select(item=>(string)Get(item,"id")).ToArray();
    static string Export()=>(string)Call(Game(),"ExportJourneyState");
    static JArray VisibleCensus()
    {
        Call(Game(),"LateUpdate");var result=new JArray();
        foreach(var view in ((IDictionary)Get(Game(),"pixelActorViews")).Values.Cast<object>())
        {
            var actor=Get(view,"actor");bool physical=Root(actor).gameObject.activeInHierarchy,pixel=((RectTransform)Get(view,"root")).gameObject.activeInHierarchy;
            if(physical||pixel)result.Add(new JObject{["id"]=(string)Get(view,"id"),["name"]=(string)Get(Get(actor,"person"),"name"),["role"]=(string)Get(view,"role"),["physicalVisible"]=physical,["pixelVisible"]=pixel});
        }
        return result;
    }
    static bool CensusClear(JArray census)=>census.All(item=>new[]{"seated","barseated","bartender","server"}.Contains((string)item["role"])||((string)item["role"]).StartsWith("hired-"));
    static void Resume(string value){Call(Game(),"ImportJourneyState",value);Call(Game(),"ContinueJourney");}
    static void Fresh()
    {
        var game=Game();Set(Journey(),"inProgress",false);Set(Journey(),"tutorialComplete",true);Call(game,"StartFreshJourneyNight",false);Set(game,"paused",true);SimBarLocalization.SetLanguage(true);Call(game,"LateUpdate");
    }
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText("Library/SimBarQA/ShowFinale-GameView.json",JsonConvert.SerializeObject(new{index=type.GetProperty("selectedSizeIndex",B).GetValue(window)}));type.GetProperty("selectedSizeIndex",B).SetValue(window,1);return new{saved=true};
    }
    public static object Orders()
    {
        Fresh();var game=Game();var patrons=(IList)Get(game,"patrons");foreach(object patron in patrons)Call(game,"ClearOrder",patron);
        var queue=Get(game,"orderQueue");Call(queue,"Clear");var names=(string[])typeof(SimBarExperience).GetProperty("DrinkNames",B).GetValue(null);var actors=new List<object>();
        for(int i=0;i<names.Length;i++){var patron=patrons[8+i];Call(game,"AssignOrder",patron,i);actors.Add(((IDictionary)Get(game,"motionLookup"))[patron]);}
        Call(game,"LateUpdate");var atlas=((IDictionary)Get(game,"pixelAtlases"))["orders"];int columns=(int)Get(atlas,"columns");var entries=new JArray();
        for(int i=0;i<actors.Count;i++)
        {
            var view=View(actors[i]);var bubble=(RectTransform)Get(view,"orderBubble");var icon=(RawImage)Get(view,"orderIcon");Require(bubble.gameObject.activeSelf&&Mathf.Abs(icon.uvRect.x-i/(float)columns)<.00001f,"Wrong requested item icon");
            Require(!((Image)Get(view,"marker")).gameObject.activeSelf,"Generic order marker remains");entries.Add(new JObject{["item"]=i,["name"]=names[i],["x"]=bubble.anchoredPosition.x,["y"]=-bubble.anchoredPosition.y,["uv"]=icon.uvRect.ToString("F6")});
        }
        bool cardsClear=true;for(int i=0;i<actors.Count;i++)for(int j=i+1;j<actors.Count;j++){Vector2 a=((RectTransform)Get(View(actors[i]),"orderBubble")).anchoredPosition,b=((RectTransform)Get(View(actors[j]),"orderBubble")).anchoredPosition;if(Mathf.Abs(a.x-b.x)<34f&&Mathf.Abs(a.y-b.y)<30f)cardsClear=false;}Require(cardsClear,"Nearby order cards overlap");
        Require(((Transform)Get(game,"pixelOrderBubbles")).GetSiblingIndex()>((Transform)Get(game,"pixelAirborne")).GetSiblingIndex(),"Bubble layer is under paper");
        File.WriteAllText(Dir+"ordered-actors.json",JsonConvert.SerializeObject(actors.Select(a=>(int)Get(a,"index"))));
        return Record("all-menu-order-bubbles",new{count=entries.Count,columns,cardsClear,entries},true);
    }
    public static object OrderLifecycle()
    {
        var game=Game();var patrons=(IList)Get(game,"patrons");object first=patrons[8],second=patrons[9],moving=patrons[10];var lookup=(IDictionary)Get(game,"motionLookup");var movingView=View(lookup[moving]);Vector2 before=((RectTransform)Get(movingView,"orderBubble")).anchoredPosition;
        var queue=Get(game,"orderQueue");Call(queue,"Clear");for(int i=0;i<3;i++)Call(queue,"Enqueue",patrons[8+i]);for(int i=11;i<14;i++)Call(game,"ClearOrder",patrons[i]);
        Set(game,"paused",false);Call(game,"ResolveDrinkService",1,true,1f);Require((int)Get(first,"drinkIndex")==0,"Wrong item removed an order");
        int seed=1;for(;seed<10000;seed++){UnityEngine.Random.InitState(seed);if(UnityEngine.Random.Range(0,patrons.Count)==8)break;}Require(seed<10000,"Could not force replacement choice");
        UnityEngine.Random.InitState(seed);UnityEngine.Random.Range(0,patrons.Count);UnityEngine.Random.Range(0,5);UnityEngine.Random.Range(52f,68f);string expectedRandom=JsonUtility.ToJson(UnityEngine.Random.state);
        UnityEngine.Random.InitState(seed);Call(game,"ResolveDrinkService",0,true,1f);Require((int)Get(first,"drinkIndex")==-1,"Served guest was immediately selected again");bool rngPreserved=expectedRandom==JsonUtility.ToJson(UnityEngine.Random.state);Require(rngPreserved,"Replacement consumed extra random draws");
        Set(second,"patience",0f);Call(game,"UpdatePatience",.1f);for(int i=0;i<24;i++)Call(game,"TickVenueMotion",.1f);Set(game,"paused",true);Call(game,"LateUpdate");
        bool servedHidden=!((RectTransform)Get(View(lookup[first]),"orderBubble")).gameObject.activeSelf,expiredHidden=!((RectTransform)Get(View(lookup[second]),"orderBubble")).gameObject.activeSelf,followed=before!=((RectTransform)Get(movingView,"orderBubble")).anchoredPosition;
        Require(servedHidden&&expiredHidden&&followed,"Order bubble lifecycle or following failed");Vector2 point=((RectTransform)Get(movingView,"orderBubble")).anchoredPosition;Rect uv=((RawImage)Get(movingView,"orderIcon")).uvRect;for(int i=0;i<8;i++){Call(game,"TickVenueMotion",.1f);Call(game,"LateUpdate");}
        bool paused=point==((RectTransform)Get(movingView,"orderBubble")).anchoredPosition&&uv==((RawImage)Get(movingView,"orderIcon")).uvRect;Require(paused,"Paused order bubble moved");
        return Record("order-lifecycle",new{wrongServicePreserved=true,servedHidden,expiredHidden,followed,pauseFrozen=paused,forcedReplacementSeed=seed,samePatronExcluded=true,rngPreserved},true);
    }
    public static object BeginClosing()
    {
        var game=Game();Set(game,"paused",false);for(int i=0;i<70;i++)Call(game,"TickVenueMotion",.1f);Set(Journey(),"floorMissions",SimBarFloorMissions.NewState());Set(Journey(),"secondsLeft",0f);Call(game,"FinishJourneyNight");
        Require((bool)Get(game,"musicPaused")&&(bool)Get(Journey(),"musicPaused")&&(int)Get(Journey(),"musicPauseSession")== (int)Get(Journey(),"session"),"Performance music did not pause once");
        var music=GameObject.Find("Closing Music Play Pause").GetComponent<Button>();var musicCanvas=(Canvas)Get(game,"juiClosingMusicCanvas");var floorCanvas=(Canvas)Get(game,"floorMissionCanvas");Require(music.gameObject.activeInHierarchy&&music.interactable&&musicCanvas.sortingOrder>floorCanvas.sortingOrder,"Closing music is covered by floor prompts");music.onClick.Invoke();Require(!(bool)Get(Journey(),"musicPaused"),"Manual closing music resume failed");Call(game,"TryBeginFloorClosing");Require(!(bool)Get(Journey(),"musicPaused"),"Repeat closing paused manually resumed music");
        string saved=Export();Resume(saved);Require(!(bool)Get(Journey(),"musicPaused"),"Saved manual resume was lost");Set(game,"paused",true);Call(game,"LateUpdate");
        bool ordersHidden=((IDictionary)Get(game,"pixelActorViews")).Values.Cast<object>().All(v=>!((RectTransform)Get(v,"orderBubble")).gameObject.activeSelf);Require(ordersHidden,"Closing retained order bubbles");
        return Record("closing-music",new{audience=Audience().Count,members=Members().Count,ordersHidden,musicPaused=(bool)Get(Journey(),"musicPaused"),musicPauseSession=Get(Journey(),"musicPauseSession"),surferY=Position(Actor("crowd-surfer")).y},true);
    }
    public static object GreetingBarrier()
    {
        var game=Game();Set(game,"paused",false);bool heldEarly=false;float maxStep=0f;int frames=0;
        for(;frames<2500;frames++)
        {
            var before=Ids().ToDictionary(id=>id,id=>Position(Actor(id)));Step();bool all=Members().Cast<object>().All(m=>(int)Get(m,"greeting")>=2||(string)Get(m,"phase")=="gone");
            foreach(string id in before.Keys)maxStep=Mathf.Max(maxStep,Vector3.Distance(before[id],Position(Actor(id))));
            if(!all){Require(Items().All(item=>(string)Get(item,"phase")!="exit"&&(string)Get(item,"phase")!="gone"),"Someone departed before every greeting");heldEarly|=Members().Cast<object>().Any(m=>(string)Get(m,"phase")=="await-exit");}
            if((bool)Get(State(),"departureStarted"))break;
        }
        Set(game,"paused",true);Require(frames<2500&&heldEarly&&maxStep<=.251f,"Greeting barrier or physical movement failed");Require(Position(Actor("crowd-surfer")).y<=.01f,"Surfer left before being lowered");
        File.WriteAllText(Dir+"barrier-save.json",Export());return Record("all-greetings-before-departure",new{frames,heldEarly,maxStep,allFourFinished=Members().Cast<object>().All(m=>(int)Get(m,"greeting")==2),surferGrounded=true},true);
    }
    public static object MidExitSave()
    {
        var game=Game();Resume(File.ReadAllText(Dir+"barrier-save.json"));Set(game,"paused",false);int frames=0;for(;frames<2000;frames++){Step();string chosen=(string)Get(State(),"departingActor");if(Items().Count(item=>(string)Get(item,"phase")=="gone")>=4&&!string.IsNullOrEmpty(chosen)&&(bool)Get(Actor(chosen),"moving"))break;}Set(game,"paused",true);
        string active=(string)Get(State(),"departingActor");var before=Ids().ToDictionary(id=>id,id=>new{position=Position(Actor(id)),visible=Root(Actor(id)).gameObject.activeSelf});string saved=Export();Resume(saved);Set(game,"paused",true);
        bool exact=!string.IsNullOrEmpty(active)&&active==(string)Get(State(),"departingActor")&&before.All(pair=>pair.Value.position==Position(Actor(pair.Key))&&pair.Value.visible==Root(Actor(pair.Key)).gameObject.activeSelf);Require(exact,"Mid-exit save changed position/visibility/queue");
        File.WriteAllText("Library/SimBarQA/ShowFinale-ExitMetrics.json","{\"frames\":0,\"maxStep\":0,\"hiddenAtDoor\":true}");return Record("mid-exit-resume",new{frames,active,exact,gone=Items().Count(item=>(string)Get(item,"phase")=="gone")},true);
    }
    public static object Farewell()
    {
        var game=Game();Set(game,"paused",false);var metrics=JObject.Parse(File.ReadAllText("Library/SimBarQA/ShowFinale-ExitMetrics.json"));int frames=(int)metrics["frames"];float maxStep=(float)metrics["maxStep"];bool hiddenAtDoor=(bool)metrics["hiddenAtDoor"];
        for(int chunk=0;chunk<200;chunk++,frames++)
        {
            var before=Ids().ToDictionary(id=>id,id=>new{p=Position(Actor(id)),visible=Root(Actor(id)).gameObject.activeSelf});Step();foreach(var pair in before){var a=Actor(pair.Key);Vector3 p=Position(a);maxStep=Mathf.Max(maxStep,Vector3.Distance(pair.Value.p,p));if(pair.Value.visible&&!Root(a).gameObject.activeSelf)hiddenAtDoor&=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(8.8f,-5.55f))<=.181f;}
            if((bool)Get(State(),"completed"))break;
        }
        metrics["frames"]=frames;metrics["maxStep"]=maxStep;metrics["hiddenAtDoor"]=hiddenAtDoor;File.WriteAllText("Library/SimBarQA/ShowFinale-ExitMetrics.json",metrics.ToString(Formatting.None));
        if(!(bool)Get(State(),"completed"))
        {
            Set(game,"paused",true);string active=(string)Get(State(),"departingActor");var actor=string.IsNullOrEmpty(active)?null:Actor(active);object route=actor!=null&&((IDictionary)Get(game,"afterShowRoutes")).Contains(actor)?((IDictionary)Get(game,"afterShowRoutes"))[actor]:null;
            Vector3 p=actor==null?Vector3.zero:Position(actor);return Record("departure-progress",new{pending=true,frames,active,position=new[]{p.x,p.y,p.z},gone=Items().Count(item=>(string)Get(item,"phase")=="gone"),blocked=route==null?0f:(float)Get(route,"blocked"),cursor=route==null?0:(int)Get(route,"cursor"),points=route==null?0:((IList)Get(route,"points")).Count});
        }
        Require(maxStep<=.251f&&hiddenAtDoor,"Physical departure jumped or hid away from door");Require((bool)Get(game,"paused"),"Farewell did not pause");Call(game,"RefreshJourneyHUD");
        Require(Items().All(item=>(string)Get(item,"phase")=="gone"&&!Root(Actor((string)Get(item,"id"))).gameObject.activeSelf),"A performer/audience member remained");
        var views=((IDictionary)Get(game,"pixelActorViews")).Values.Cast<object>().Where(v=>Get(v,"seat")!=null).ToArray();Require(views.Length==11&&views.All(v=>Root(Get(v,"actor")).gameObject.activeSelf),"Bar/lounge guests were removed");
        var patrons=(IList)Get(game,"patrons");Require(patrons.Cast<object>().All(p=>!((Transform)Get(p,"root")).gameObject.activeSelf),"A stage-front patron was omitted from departures");var census=VisibleCensus();Require(CensusClear(census),"Rendered floor/pit/support/surfer actor remains: "+census.ToString(Formatting.None));
        string exact="오늘의 라이브 밴드 공연은 하얗게 불태웠어요! 내일 또 공연 보러와요!";Require(((Text)Get(game,"juiFarewellMessage")).text==exact&&((GameObject)Get(game,"juiFarewell")).activeSelf,"Farewell message differs");
        Call(game,"TogglePause");Require((bool)Get(game,"paused")&&!(bool)Get(State(),"noticeAcknowledged"),"P dismissed farewell");
        string saved=Export();Resume(saved);Require((bool)Get(game,"paused")&&!(bool)Get(State(),"noticeAcknowledged"),"Pending popup save did not return");
        File.WriteAllText(Dir+"farewell-save.json",Export());return Record("farewell-popup",new{frames,maxStep,hiddenAtDoor,gone=Items().Count(),allActualPatronsGone=patrons.Count,seatedRemain=views.Length,census,message=exact,pauseProtected=true,savedPopupRestored=true},true);
    }
    public static object ClearedFloor()
    {
        var census=VisibleCensus();Require(CensusClear(census),"Rendered floor is not clear");((GameObject)Get(Game(),"juiFarewell")).SetActive(false);return Record("cleared-floor",new{allActualPatronsGone=((IList)Get(Game(),"patrons")).Count,participantsGone=Items().Count(),visibleFloorActors=0,seatedRemain=census.Count(item=>(string)item["role"]=="seated"||(string)item["role"]=="barseated"),census},true);
    }
    public static object Acknowledge()
    {
        var game=Game();Call(game,"RefreshJourneyHUD");bool initial=(bool)Get(Journey(),"musicPaused");var audio=(AudioSource)Get(game,"musicSource");Call(game,"TogglePlaylistMusic");bool actualPaused=!audio.isPlaying;Call(game,"TogglePlaylistMusic");bool actualResumed=audio.isPlaying;Require((bool)Get(Journey(),"musicPaused")==initial&&actualPaused&&actualResumed,"Popup music/audio controls did not round-trip");
        Call(game,"AcknowledgeAfterShowFarewell");Require(Get(game,"journeyPhase").ToString()=="Results","Acknowledgement did not settle clear floor");float cash=(float)Get(game,"cash");int nights=(int)Get(Journey(),"completedNights");Call(game,"AcknowledgeAfterShowFarewell");Require(cash==(float)Get(game,"cash")&&nights==(int)Get(Journey(),"completedNights"),"Duplicate acknowledgement paid again");
        string saved=Export();Resume(saved);Require((bool)Get(State(),"noticeAcknowledged")&&!((GameObject)Get(game,"juiFarewell")).activeSelf&&cash==(float)Get(game,"cash"),"Acknowledged save replayed popup/payment");
        return Record("acknowledged-results",new{phase=Get(game,"journeyPhase").ToString(),idempotent=true,musicChoicePreserved=(bool)Get(Journey(),"musicPaused")==initial,actualAudioPaused=actualPaused,actualAudioResumed=actualResumed},true);
    }
    public static object PendingCleanup()
    {
        var game=Game();var json=JObject.Parse(File.ReadAllText(Dir+"farewell-save.json"));int session=(int)json["session"]+1;json["session"]=session;json["settledSession"]=session-1;json["musicPauseSession"]=session;json["floorClosing"]=true;json["inProgress"]=true;json["afterShow"]["session"]=session;
        var floor=SimBarFloorMissions.NewState();floor.tissueThrows=1;floor.litter=32;floor.cleanupStarted=true;floor.cleanupWorkers=2;floor.cleanupActive=true;floor.reaction=SimBarFloorChallenges.NewState(7,180f);json["floorMissions"]=JObject.FromObject(floor);Resume(json.ToString(Formatting.None));
        Require((bool)Get(game,"paused"),"Pending cleanup popup not paused");Call(game,"TogglePause");Call(game,"AcknowledgeAfterShowFarewell");Require(Get(game,"journeyPhase").ToString()=="FloorClosing"&&!(bool)Get(game,"paused"),"Ack left pending cleanup paused");
        int frames=0;for(;frames<2000;frames++){Step();var state=(SimBarFloorMissions.State)Get(Journey(),"floorMissions");if(state.reaction!=null&&state.reaction.activeKind==SimBarFloorChallenges.Cleanup)Call(game,"FloorQuickPress",SimBarFloorChallenges.Cleanup);if(Get(game,"journeyPhase").ToString()=="Results")break;}
        Set(game,"paused",true);Require(frames<2000,"Cleanup/staff return did not settle after farewell acknowledgement");return Record("pending-cleanup-after-ack",new{frames,phase=Get(game,"journeyPhase").ToString(),cleanupComplete=((SimBarFloorMissions.State)Get(Journey(),"floorMissions")).cleanupComplete},true);
    }
    public static object LegacyMigration()
    {
        Fresh();var game=Game();Set(game,"paused",false);Set(Journey(),"floorMissions",SimBarFloorMissions.NewState());Call(game,"FinishJourneyNight");Set(game,"paused",true);var json=JObject.Parse(Export());var old=(JObject)json["afterShow"];old["version"]=1;old.Remove("departureStarted");old.Remove("departingActor");old.Remove("noticeAcknowledged");old["audience"]=new JArray(((JArray)old["audience"]).Take(8).Select(item=>item.DeepClone()));old["members"][0]["phase"]="exit";old["members"][0]["greeting"]=2;old["members"][1]["phase"]="gone";old["members"][1]["greeting"]=2;old["audience"][0]["phase"]="done";old["audience"][1]["phase"]="return";json.Remove("musicPauseSession");json.Remove("musicPaused");
        Call(game,"ImportJourneyState",json.ToString(Formatting.None));Require((int)Get(State(),"version")==2&&Audience().Count==((IList)Get(game,"patrons")).Count+4&&(string)Get(Members()[0],"phase")=="await-exit"&&(string)Get(Members()[1],"phase")=="gone"&&Audience().Cast<object>().Take(2).All(a=>(string)Get(a,"phase")=="farewell-wait"),"Active v1 migration failed");Require((bool)Get(Journey(),"musicPaused"),"Legacy closing did not receive one music pause during import");Call(game,"ContinueJourney");Require((bool)Get(Journey(),"musicPaused"),"Legacy closing did not preserve music pause");
        json["inProgress"]=false;json["floorClosing"]=false;json["settledSession"]=json["session"];old["completed"]=true;Call(game,"ImportJourneyState",json.ToString(Formatting.None));Call(game,"RestoreAfterShow");Require((bool)Get(State(),"completed")&&(bool)Get(State(),"noticeAcknowledged")&&Items().All(item=>(string)Get(item,"phase")=="gone"),"Settled v1 migration replayed farewell");
        return Record("legacy-migration",new{activeV1Migrated=true,oldGonePreserved=true,legacyMusicPausedOnce=true,settledV1Acknowledged=true,settledAudienceGone=Audience().Count});
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/ShowFinale-GameView.json"));type.GetProperty("selectedSizeIndex",B).SetValue(window,(int)viewport["index"]);return Readback();
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/ShowFinale-GameView.json"));
        Directory.CreateDirectory(Dir);var result=new{prefsRestored=exact,gameViewRestored=(int)type.GetProperty("selectedSizeIndex",B).GetValue(window)==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling};File.WriteAllText(Dir+"preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
