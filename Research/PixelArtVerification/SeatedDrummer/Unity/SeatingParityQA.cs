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

public static class SeatingParityQA
{
    const string Dir="Research/PixelArtVerification/SeatedDrummer/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static void Set(object target,string key,object value)=>target.GetType().GetField(key,B).SetValue(target,value);
    static object Call(object target,string key,params object[] args){try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static object View(string id){foreach(DictionaryEntry entry in (IDictionary)Get(Game(),"pixelActorViews"))if((string)Get(entry.Value,"id")==id)return entry.Value;throw new Exception("Missing view "+id);}
    static object Actor(string id)=>Get(View(id),"actor");
    static Transform Root(object actor)=>(Transform)Get(Get(actor,"person"),"root");
    static object Record(string name,object value,bool capture=false){Directory.CreateDirectory(Dir);File.WriteAllText(Dir+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"),2);return value;}
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));return new{saved=true};
    }
    public static object Owners()
    {
        var portraits=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Portrait "+i).GetComponent<RawImage>()).Select(i=>new{name=i.name,width=i.texture.width,height=i.texture.height,drawWidth=i.rectTransform.sizeDelta.x,drawHeight=i.rectTransform.sizeDelta.y}).ToArray();
        return Record("owners",new{selectedOwner=Game().SelectedOwnerId,portraits},true);
    }
    public static object Performance()
    {
        var game=Game();Call(game,"ResetAfterShow");var journey=Get(game,"journey");Set(journey,"inProgress",true);Set(journey,"secondsLeft",180f);Set(journey,"floorClosing",false);Set(journey,"guidePending",false);
        Set(game,"journeyPhase",Enum.Parse(Get(game,"journeyPhase").GetType(),"Playing"));Set(game,"paused",true);Set(game,"pixelAnimationSeconds",1.2f);Call(game,"RefreshJourneyHUD");
        var positions=new Dictionary<object,Vector3>();foreach(var actor in (IList)Get(game,"motionActors"))if(Root(actor)!=null)positions[actor]=Root(actor).position;
        Call(game,"LateUpdate");Canvas.ForceUpdateCanvases();
        var seats=new JArray();bool anchors=true,depths=true,shadows=true,masks=true;
        foreach(DictionaryEntry entry in (IDictionary)Get(game,"pixelActorViews"))
        {
            var view=entry.Value;var seat=Get(view,"seat");if(seat==null)continue;
            var actor=Get(view,"actor");Vector3 before=positions[actor];var root=(RectTransform)Get(view,"root");var point=root.anchoredPosition;var feet=(Vector2)Get(seat,"feet");var layer=Get(view,"layer");var after=(IList)Get(layer,"after");
            bool anchor=point==new Vector2(Mathf.Round(feet.x),-Mathf.Round(feet.y)),depth=(float)Get(layer,"depth")== (float)Get(seat,"depth"),hidden=!((SimBarPixelPolygonGraphic)Get(view,"shadow")).gameObject.activeSelf;
            bool ordered=after!=null&&after.Count>0;for(int i=0;after!=null&&i<after.Count;i++){var mask=(Transform)after[i];ordered&=mask.gameObject.activeSelf&&mask.GetSiblingIndex()==root.GetSiblingIndex()+i+1;}
            anchors&=anchor;depths&=depth;shadows&=hidden;masks&=ordered;
            seats.Add(new JObject{["id"]=(string)Get(view,"id"),["x"]=point.x,["y"]=-point.y,["depth"]=(float)Get(layer,"depth"),["shadowHidden"]=hidden,["targetedMaskImmediatelyAfter"]=ordered,["worldPositionUnchanged"]=before==Root(actor).position});
        }
        var drummer=View("drummer");var drumPoint=((RectTransform)Get(drummer,"root")).anchoredPosition;float drumDepth=(float)Get(Get(drummer,"layer"),"depth");
        return Record("performance",new{seatCount=seats.Count,anchors,depths,shadows,masks,globalLayers=((IList)Get(game,"pixelStaticLayers")).Count,drummerX=drumPoint.x,drummerY=-drumPoint.y,drummerDepth=drumDepth,drummerBehindKit=drumDepth<310f,seats},true);
    }
    public static object StandingGuard()
    {
        var game=Game();var view=View("bar-seat-0");var actor=Get(view,"actor");var kind=Get(actor,"kind");Vector3 position=Root(actor).position;var world=(Transform)Get(game,"worldRoot");
        Set(actor,"kind",Enum.Parse(kind.GetType(),"Guest"));Set(actor,"moving",true);Root(actor).position=world.TransformPoint(new Vector3(-7.3f,0f,-3.15f));Call(game,"LateUpdate");
        var point=((RectTransform)Get(view,"root")).anchoredPosition;var feet=(Vector2)Get(Get(view,"seat"),"feet");var after=(IList)Get(Get(view,"layer"),"after");bool masksOff=true;foreach(Transform mask in after)masksOff&=!mask.gameObject.activeSelf;
        bool movingUnpinned=point!=new Vector2(feet.x,-feet.y),shadowVisible=((SimBarPixelPolygonGraphic)Get(view,"shadow")).gameObject.activeSelf;
        Set(actor,"moving",false);Call(game,"LateUpdate");bool standingUnpinned=((RectTransform)Get(view,"root")).anchoredPosition!=new Vector2(feet.x,-feet.y);
        Set(actor,"kind",kind);Root(actor).position=position;Call(game,"LateUpdate");
        return Record("standing-guard",new{movingUnpinned,standingUnpinned,masksOff,shadowVisible,restoredAnchor=((RectTransform)Get(view,"root")).anchoredPosition==new Vector2(feet.x,-feet.y)});
    }
    public static object Pause()
    {
        var game=Game();Set(game,"paused",true);float clock=(float)Get(game,"pixelAnimationSeconds");var before=JsonConvert.SerializeObject(((IDictionary)Get(game,"pixelActorViews")).Values.Cast<object>().Select(v=>new{point=((RectTransform)Get(v,"root")).anchoredPosition.ToString("F4"),frame=((RawImage)Get(v,"image")).uvRect.ToString("F6")}));
        for(int i=0;i<8;i++)Call(game,"LateUpdate");var after=JsonConvert.SerializeObject(((IDictionary)Get(game,"pixelActorViews")).Values.Cast<object>().Select(v=>new{point=((RectTransform)Get(v,"root")).anchoredPosition.ToString("F4"),frame=((RawImage)Get(v,"image")).uvRect.ToString("F6")}));
        return Record("pause",new{clockFrozen=clock==(float)Get(game,"pixelAnimationSeconds"),actorsFrozen=before==after});
    }
    public static object BeginAfterShow()
    {
        var game=Game();var journey=Get(game,"journey");Set(journey,"floorMissions",SimBarFloorMissions.NewState());Set(journey,"secondsLeft",0f);Call(game,"FinishJourneyNight");Set(game,"paused",true);return new{started=true};
    }
    static object UntilDrummer(int target)
    {
        var game=Game();var actor=Actor("drummer");var state=Get(Get(game,"journey"),"afterShow");object member=((IList)Get(state,"members")).Cast<object>().First(m=>(string)Get(m,"id")=="drummer");
        Set(game,"paused",false);int frames=0;bool reached=false;float depth=0f,level=0f;Vector2 point=Vector2.zero;
        for(;frames<1800;frames++)
        {
            Call(game,"TickVenueMotion",.1f);Call(game,"TickAfterShow",.1f);Call(game,"LateUpdate");var view=View("drummer");depth=(float)Get(Get(view,"layer"),"depth");level=(float)Get(member,"stageLevel");point=((RectTransform)Get(view,"root")).anchoredPosition;
            bool walking=(bool)Get(actor,"moving");reached=target==0?walking&&level>.95f&&depth<310f:target==1?walking&&level>.95f&&depth>310f&&-point.y>310f:walking&&level<.01f;
            if(reached)break;
        }
        Set(game,"paused",true);Call(game,"LateUpdate");string name=target==0?"drummer-behind-kit":target==1?"drummer-front-kit":"drummer-floor";
        return Record(name,new{reached,frames,stageLevel=level,depth,x=point.x,y=-point.y,phase=(string)Get(member,"phase")},true);
    }
    public static object DrummerBehind()=>UntilDrummer(0);
    public static object DrummerFront()=>UntilDrummer(1);
    public static object DrummerFloor()=>UntilDrummer(2);
    public static object FinishAfterShow()
    {
        var game=Game();Set(game,"paused",false);int frames=0;for(;frames<1800;frames++){Call(game,"TickVenueMotion",.1f);Call(game,"TickAfterShow",.1f);Call(game,"TickFloorMissions",.1f);if(Get(game,"journeyPhase").ToString()=="Results")break;}Set(game,"paused",true);
        var state=Get(Get(game,"journey"),"afterShow");return Record("aftershow-finish",new{frames,phase=Get(game,"journeyPhase").ToString(),complete=(bool)Get(state,"completed"),allBandGone=((IList)Get(state,"members")).Cast<object>().All(m=>(string)Get(m,"phase")=="gone"),allFansHome=((IList)Get(state,"audience")).Cast<object>().All(a=>(string)Get(a,"phase")=="done")});
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return Record("preservation",new{prefsRestored=exact,scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground});
    }
}
