using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class JessyChibiQA
{
    const string Dir="Research/PixelArtVerification/JessyChibi/Unity/";
    const string ViewBackup="Library/SimBarQA/JessyChibi-GameView.json";
    const string OwnedPreset="SimBar temporary Jessy chibi QA";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object x,string key){var f=x.GetType().GetField(key,B);return f!=null?f.GetValue(x):x.GetType().GetProperty(key,B).GetValue(x);}
    static void Set(object x,string key,object value)=>x.GetType().GetField(key,B).SetValue(x,value);
    static object Call(object x,string key,params object[] args){try{return x.GetType().GetMethod(key,B).Invoke(x,args);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();return null;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static void Require(bool v,string message){if(!v)throw new Exception(message);}
    static object Record(string key,object result,bool capture=false){Directory.CreateDirectory(Dir);Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+key+".json",JsonConvert.SerializeObject(result,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+key+".png"));return result;}
    static Type ViewType()=>typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
    static object View()=>Resources.FindObjectsOfTypeAll(ViewType()).First();
    static object Group(){var t=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");return Call(t.GetProperty("instance",B|BindingFlags.FlattenHierarchy).GetValue(null),"GetGroup",ViewType().GetProperty("currentSizeGroupType",B).GetValue(View()));}
    static object[] Presets(){var g=Group();return Enumerable.Range(0,(int)Call(g,"GetBuiltinCount")+(int)Call(g,"GetCustomCount")).Select(i=>{var s=Call(g,"GetGameViewSize",i);return (object)new{index=i,name=((string[])Call(g,"GetDisplayTexts"))[i],width=(int)Get(s,"width"),height=(int)Get(s,"height"),type=Get(s,"sizeType").ToString()};}).ToArray();}
    static void Click(Button b){Require(b.gameObject.activeInHierarchy&&b.interactable,"Button unavailable: "+b.name);ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
    public static object ImportAssets()
    {
        foreach(string path in new[]{"Assets/Art/StartupTextures/Owners/OwnerFemale.png","Assets/Resources/SimBarWebTextures/OwnerFemale.bytes","Assets/Resources/SimBarOffice/desk-jessy.bytes"})UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceUpdate);
        var texture=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/StartupTextures/Owners/OwnerFemale.png");Require(texture.width==400&&texture.height==350&&texture.filterMode==FilterMode.Point,"Selector import settings changed");return new{imported=3,point=true};
    }
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);Require(!File.Exists(Dir+"prefs-backup.json"),"Keep original preference backup intact.");File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText(ViewBackup,JsonConvert.SerializeObject(new{index=ViewType().GetProperty("selectedSizeIndex",B).GetValue(View()),custom=(int)Call(Group(),"GetCustomCount"),presets=Presets(),runInBackground=UnityEditor.PlayerSettings.runInBackground}));return new{saved=true};
    }
    static object Size(int w,int h)
    {
        var g=Group();var assembly=typeof(UnityEditor.EditorWindow).Assembly;Type t=assembly.GetType("UnityEditor.GameViewSize"),kind=assembly.GetType("UnityEditor.GameViewSizeType");var size=Activator.CreateInstance(t,B,null,new object[]{Enum.Parse(kind,"FixedResolution"),w,h,OwnedPreset},null);Call(g,"AddCustomSize",size);int index=(int)Call(g,"GetBuiltinCount")+(int)Call(g,"GetCustomCount")-1;ViewType().GetProperty("selectedSizeIndex",B).SetValue(View(),index);return new{width=w,height=h,index};
    }
    public static object Desktop()=>Size(1280,800);
    public static object Mobile()=>Size(390,844);
    public static object Start()
    {
        var g=Game();g.enabled=false;Call(g,"ImportJourneyState","{\"version\":1,\"tutorialComplete\":true}");Call(g,"JourneyReturnToLobby");SimBarLocalization.SetLanguage(true);return new{ready=true};
    }
    static object Selector(int owner,string name)
    {
        var g=Game();Call(g,"JourneyReturnToLobby");Click(((Button[])Get(g,"ownerCards"))[owner]);Call(g,"LoadOwnerSelection");Call(g,"RefreshOwnerSelection");Require(g.SelectedOwnerIndex==owner,"Owner identity did not persist");
        var portraits=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Portrait "+i).GetComponent<RawImage>()).ToArray();foreach(var p in portraits)Require(p.texture!=null&&p.texture.width==400&&p.texture.height==350&&p.texture.filterMode==FilterMode.Point&&p.uvRect==new Rect(0,0,1,1)&&p.rectTransform.sizeDelta==new Vector2(400,350),"Owner source was filtered, cropped or resized in the card");
        var badges=(Text[])Get(g,"ownerBadges");for(int i=0;i<3;i++){var rect=(RectTransform)badges[i].transform.parent;Require(rect.gameObject.activeSelf&&rect.anchoredPosition==new Vector2(-139,193)&&rect.sizeDelta==new Vector2(104,30),"Shared owner badge changed");}
        return Record(name,new{selectedOwner=g.SelectedOwnerId,viewport=new{Screen.width,Screen.height},allThreeOriginalAspect=true,identityUvNoCrop=true,pointFilter=true,sharedBadges=true,portraits=portraits.Select(p=>new{name=p.name,texture=p.texture.name,width=p.texture.width,height=p.texture.height,uv=p.uvRect.ToString("F6")})},true);
    }
    public static object SelectorJessy()=>Selector(0,"selector-desktop-jessy");
    public static object SelectorEdison()=>Selector(1,"selector-desktop-edison");
    public static object SelectorJaiden()=>Selector(2,"selector-desktop-jaiden");
    public static object SelectorMobile()=>Selector(0,"selector-mobile-jessy");
    public static object SelectorMobileEdison()=>Selector(1,"selector-mobile-edison");
    public static object SelectorMobileJaiden()=>Selector(2,"selector-mobile-jaiden");
    static object Office(string name)
    {
        var g=Game();Call(g,"JourneyReturnToLobby");Click(((Button[])Get(g,"ownerCards"))[0]);Call(g,"ImportJourneyState","{\"version\":1,\"tutorialComplete\":true}");Call(g,"BeginDayPlanning",false);Call(Get(g,"bookingLayout"),"RefreshLayout");Call(g,"SetDeskChannel",1);var image=(RawImage)Get(g,"deskArtImage");Require(g.SelectedOwnerIndex==0&&(int)Get(g,"deskArtOwner")==0&&(bool)Get(g,"deskOfficeReady")&&image.texture.width==640&&image.texture.height==360&&image.texture.filterMode==FilterMode.Point&&image.uvRect==new Rect(0,0,1,1),"Jessy office identity/fit differs");
        var frame=(RectTransform)Get(g,"deskArtFrame");var caption=(RectTransform)Get(g,"deskCaptionRect");Require(-caption.anchoredPosition.y>=-frame.anchoredPosition.y+frame.rect.height*frame.localScale.y,"Caption covers the desk phone");var plan=(SimBarBooking.Plan)Get(Get(g,"journey"),"booking");Require(SimBarBooking.IsDesk(plan)&&!plan.freewillSelected&&plan.deskDoneMask==0,"Visual check changed the planning state");
        return Record(name,new{owner=g.SelectedOwnerId,resource="SimBarOffice/desk-jessy",viewport=new{Screen.width,Screen.height},whole640x360Image=true,identityUvNoCrop=true,pointFilter=true,captionBelowImage=true,freewillUiUnchanged=true},true);
    }
    public static object OfficeDesktop()=>Office("office-desktop-jessy");
    public static object OfficeMobile()=>Office("office-mobile-jessy");
    public static object SilenceForStop(){foreach(var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))source.Stop();AudioListener.pause=true;return new{qaOnlyAudioStopped=true};}
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var x in saved){string key=(string)x["key"];if((bool)x["exists"])PlayerPrefs.SetString(key,(string)x["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();var config=JObject.Parse(File.ReadAllText(ViewBackup));
        try
        {
            var group=Group();ViewType().GetProperty("selectedSizeIndex",B).SetValue(View(),(int)config["index"]);int builtin=(int)Call(group,"GetBuiltinCount"),total=builtin+(int)Call(group,"GetCustomCount");Require(total<100,"Unexpected viewport count");
            for(int i=total-1;i>=builtin+(int)config["custom"];i--)
            {
                var s=Call(group,"GetGameViewSize",i);string name=((string[])Call(group,"GetDisplayTexts"))[i];int w=(int)Get(s,"width"),h=(int)Get(s,"height");bool owned=name.StartsWith(OwnedPreset,StringComparison.Ordinal)&&Get(s,"sizeType").ToString()=="FixedResolution"&&((w==1280&&h==800)||(w==390&&h==844));if(owned){int count=(int)Call(group,"GetCustomCount");Call(group,"RemoveCustomSize",i);Require((int)Call(group,"GetCustomCount")==count-1,"Temporary viewport removal failed");}
            }
        }
        finally{UnityEditor.PlayerSettings.runInBackground=(bool)config["runInBackground"];}
        return Readback();
    }
    public static object Readback()
    {
        var original=File.ReadAllText(Dir+"prefs-backup.json");var saved=JArray.Parse(original);var current=new JArray(saved.Select(x=>new JObject{{"key",(string)x["key"]},{"exists",PlayerPrefs.HasKey((string)x["key"])},{"value",PlayerPrefs.GetString((string)x["key"])}}));string actual=current.ToString(Formatting.None);var config=JObject.Parse(File.ReadAllText(ViewBackup));var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();Func<string,string> hash=s=>BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();return Record("preservation",new{prefsByteExact=original==actual,originalPrefsSha256=hash(original),restoredPrefsSha256=hash(actual),gameViewRestored=(int)ViewType().GetProperty("selectedSizeIndex",B).GetValue(View())==(int)config["index"]&&JToken.DeepEquals(config["presets"],JToken.FromObject(Presets())),customCount=(int)Call(Group(),"GetCustomCount"),scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling});
    }
}
