using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class OwnerRefinementQA
{
    const string Dir="Research/PixelArtVerification/OwnerRefinement/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
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
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText("Library/SimBarQA/OwnerRefinement-GameView.json",JsonConvert.SerializeObject(new{index=type.GetProperty("selectedSizeIndex",B).GetValue(window)}));type.GetProperty("selectedSizeIndex",B).SetValue(window,1);return new{saved=true};
    }
    public static object GameViewInfo()
    {
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();
        var sizesType=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",B|BindingFlags.FlattenHierarchy).GetValue(null);var group=Call(sizes,"GetGroup",type.GetProperty("currentSizeGroupType",B).GetValue(window));
        return new{index=type.GetProperty("selectedSizeIndex",B).GetValue(window),sizes=Call(group,"GetDisplayTexts")};
    }
    public static object Owners()
    {
        var game=Game();SimBarLocalization.SetLanguage(true);Call(game,"RefreshOwnerSelection");Canvas.ForceUpdateCanvases();
        var descriptions=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Description "+i).GetComponent<Text>().text).ToArray();
        var expected=new[]{"자기야 오늘밤 라이브 하러와","평생 Rock&Roll","오늘밤은 무슨 음악을 들을까?"};
        if(!descriptions.SequenceEqual(expected))throw new Exception("Owner descriptions differ from requested text");
        var portraits=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Portrait "+i).GetComponent<RawImage>()).Select(i=>new{name=i.name,width=i.texture.width,height=i.texture.height,drawWidth=i.rectTransform.sizeDelta.x,drawHeight=i.rectTransform.sizeDelta.y}).ToArray();
        return Record("owners-korean",new{selectedOwner=game.SelectedOwnerId,descriptions,portraits},true);
    }
    public static object English()
    {
        SimBarLocalization.SetLanguage(false);Call(Game(),"RefreshOwnerSelection");Canvas.ForceUpdateCanvases();
        var descriptions=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Description "+i).GetComponent<Text>().text).ToArray();
        if(!descriptions.SequenceEqual(new[]{"Darling, come play live tonight.","Rock&Roll for life.","What shall we listen to tonight?"}))throw new Exception("English descriptions differ");
        return Record("owners-english",new{descriptions},true);
    }
    static void Performance()
    {
        var game=Game();Call(game,"ResetAfterShow");var journey=Get(game,"journey");Set(journey,"inProgress",true);Set(journey,"secondsLeft",180f);Set(journey,"floorClosing",false);Set(journey,"guidePending",false);
        Set(game,"journeyPhase",Enum.Parse(Get(game,"journeyPhase").GetType(),"Playing"));Set(game,"paused",true);Call(game,"RefreshJourneyHUD");Call(game,"LateUpdate");Canvas.ForceUpdateCanvases();
    }
    static object RenderState(string name)
    {
        var game=Game();Call(game,"LateUpdate");Canvas.ForceUpdateCanvases();var view=View("bartender");var actor=Get(view,"actor");var image=(RawImage)Get(view,"image");var point=((RectTransform)Get(view,"root")).anchoredPosition;
        Vector3 world=(Vector3)Call(game,"MotionPosition",actor);
        return Record(name,new{action=(string)Call(game,"PixelActionFor",actor),moving=(bool)Get(actor,"moving"),x=world.x,z=world.z,pixelX=point.x,pixelY=-point.y,texture=image.texture.name,uv=image.uvRect.ToString("F6"),depth=(float)Get(Get(view,"layer"),"depth")},true);
    }
    public static object BartenderStand()
    {
        Performance();var game=Game();var actor=Actor("bartender");var world=(Transform)Get(game,"worldRoot");Root(actor).position=world.TransformPoint(new Vector3(-8.95f,0f,-.565f));
        Set(actor,"moving",false);Set(actor,"speed",0f);Set(game,"motionClock",0f);Set(game,"pixelAnimationSeconds",0f);
        var visual=(SimBarCharacterVisual)Get(actor,"visual");if(visual!=null)visual.ApplyPresentation(0f,.4f,0f,0f);
        return RenderState("bartender-standing");
    }
    public static object BartenderServe()
    {
        var game=Game();var actor=Actor("bartender");var visual=(SimBarCharacterVisual)Get(actor,"visual");
        if(visual==null)throw new Exception("Missing managed bartender visual");Set(visual,"servingBlend",.95f);Set(visual,"walkingBlend",0f);Set(actor,"moving",false);Call(game,"LateUpdate");
        var image=(RawImage)Get(View("bartender"),"image");var textures=(IDictionary)Get(game,"pixelAtlasTextures");
        if((string)Call(game,"PixelActionFor",actor)!="serve"||image.texture!=(Texture2D)textures["wardrobe"])throw new Exception("Bar service did not retain the standing Suit frame");
        return RenderState("bartender-serving-standing");
    }
    public static object BartenderWalk()
    {
        var game=Game();var actor=Actor("bartender");Set(game,"motionClock",5f-(int)Get(actor,"index")*1.9f);Set(game,"paused",false);
        for(int i=0;i<8;i++)Call(game,"TickVenueMotion",.1f);Set(game,"paused",true);Set(game,"pixelAnimationSeconds",.15f);
        if(!(bool)Get(actor,"moving"))throw new Exception("Bartender did not walk on patrol");
        return RenderState("bartender-walking");
    }
    public static object PatrolAndPause()
    {
        var game=Game();var actor=Actor("bartender");Set(game,"paused",false);float min=float.MaxValue,max=float.MinValue,maxStep=0f;Vector3 previous=(Vector3)Call(game,"MotionPosition",actor);int moving=0,standing=0;bool inAisle=true;
        for(int i=0;i<200;i++)
        {
            Call(game,"TickVenueMotion",.1f);Vector3 p=(Vector3)Call(game,"MotionPosition",actor);min=Mathf.Min(min,p.x);max=Mathf.Max(max,p.x);maxStep=Mathf.Max(maxStep,Vector3.Distance(previous,p));previous=p;inAisle&=Mathf.Abs(p.z+.565f)<.021f;
            if((bool)Get(actor,"moving"))moving++;else standing++;
        }
        Set(game,"paused",true);Call(game,"LateUpdate");Vector3 paused=Root(actor).position;var image=(RawImage)Get(View("bartender"),"image");Rect uv=image.uvRect;float clock=(float)Get(game,"motionClock");
        for(int i=0;i<8;i++){Call(game,"TickVenueMotion",.1f);Call(game,"LateUpdate");}
        var result=new{minX=min,maxX=max,maxStep,movingFrames=moving,standingFrames=standing,inAisle,pausePositionFrozen=paused==Root(actor).position,pauseFrameFrozen=uv==image.uvRect,pauseClockFrozen=clock==(float)Get(game,"motionClock")};
        if(max-min<1.5f||maxStep>.0661f||moving==0||standing==0||!inAisle||!result.pausePositionFrozen||!result.pauseFrameFrozen||!result.pauseClockFrozen)throw new Exception(JsonConvert.SerializeObject(result));
        return Record("patrol-and-pause",result);
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/OwnerRefinement-GameView.json"));type.GetProperty("selectedSizeIndex",B).SetValue(window,(int)viewport["index"]);
        return Record("preservation",new{prefsRestored=exact,gameViewRestored=(int)type.GetProperty("selectedSizeIndex",B).GetValue(window)==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground});
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/OwnerRefinement-GameView.json"));
        return Record("editor-preserved",new{prefsRestored=exact,gameViewRestored=(int)type.GetProperty("selectedSizeIndex",B).GetValue(window)==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling});
    }
}
