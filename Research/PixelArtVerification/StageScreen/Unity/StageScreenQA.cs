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

public static class StageScreenQA
{
    const string Dir="Research/PixelArtVerification/StageScreen/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static void Set(object target,string key,object value)=>target.GetType().GetField(key,B).SetValue(target,value);
    static object Call(object target,string key,params object[] args){try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static object Journey()=>Get(Game(),"journey");
    static bool Prop(string key)=>(bool)typeof(SimBarExperience).GetProperty(key,B).GetValue(Game());
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static object Record(string name,object value,bool capture=false){Directory.CreateDirectory(Dir);Call(Game(),"LateUpdate");Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"),2);return value;}
    static void Refresh(){Call(Game(),"RefreshJourneyHUD");Call(Game(),"RefreshFloorMissionUI");Call(Game(),"LateUpdate");Canvas.ForceUpdateCanvases();}
    static IEnumerable<Light> Spots()=>((IEnumerable)Get(Game(),"stageLights")).Cast<Light>();
    static IEnumerable<Renderer> Beams()=>((IEnumerable)Get(Game(),"stageBeams")).Cast<Renderer>();
    static Light[] Practicals()=>UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>new[]{"Warm softbox","Bar amber practical","Lounge warm practical","Foreground warm practical"}.Contains(l.name)).OrderBy(l=>l.name).ToArray();
    static Button Play()=>((Text)Get(Game(),"musicPlayPauseIcon")).GetComponentInParent<Button>();
    static string[] Hits(Button button)
    {
        Canvas.ForceUpdateCanvases();var rect=button.GetComponent<RectTransform>();var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        Require(center.x>=0&&center.x<=Screen.width&&center.y>=0&&center.y<=Screen.height,"Playlist play center is outside viewport");
        var data=new PointerEventData(EventSystem.current){position=center,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Existing playlist is covered: "+string.Join(",",hits.Take(6).Select(h=>h.gameObject.name)));
        return hits.Take(6).Select(h=>h.gameObject.name).ToArray();
    }
    static void ClickPlay(){Hits(Play());ExecuteEvents.Execute(Play().gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText("Library/SimBarQA/StageScreen-GameView.json",JsonConvert.SerializeObject(new{index=type.GetProperty("selectedSizeIndex",B).GetValue(window)}));type.GetProperty("selectedSizeIndex",B).SetValue(window,1);return new{saved=true};
    }
    public static object Startup()
    {
        bool decoded=((IDictionary)Get(Game(),"pixelDecodedTextures")).Keys.Cast<string>().Any(k=>k.StartsWith("SimBarAlbumJackets/"));
        Require(!(bool)Get(Game(),"albumJacketManifestLoaded")&&!decoded,"Jackets decoded before venue entry");
        return Record("startup-lazy",new{deferred=true,albumTexturesDecoded=0});
    }
    public static object Performance()
    {
        var game=Game();Set(Journey(),"inProgress",false);Set(Journey(),"tutorialComplete",true);Call(game,"StartFreshJourneyNight",false);Set(game,"paused",true);SimBarLocalization.SetLanguage(true);Call(game,"SelectPlaylist",0);Refresh();
        Require(Prop("StagePerformanceLightingActive")&&Spots().Count()==4&&Spots().All(l=>l.enabled&&l.intensity>0f)&&Beams().All(r=>r.enabled)&&((Transform)Get(game,"pixelLightRoot")).gameObject.activeSelf,"Performance lights are missing");
        var original=new{ambient=RenderSettings.ambientLight.ToString("F6"),practicals=Practicals().Select(l=>new{l.name,l.enabled,l.intensity}).ToArray()};
        File.WriteAllText("Library/SimBarQA/StageScreen-ambient.json",JsonConvert.SerializeObject(original));
        var root=(RectTransform)Get(game,"pixelAlbumRoot");Require(root.gameObject.activeSelf&&root.GetSiblingIndex()<((Transform)Get(game,"pixelLightRoot")).GetSiblingIndex(),"Jacket is not below stage effects");
        return Record("performance-freewill",new{stageSpots=4,physicalBeams=Beams().Count(),pixelLights=true,pausedGameLightsStayVisible=true,cover=((Graphic)Get(game,"pixelAlbumImage")).mainTexture.name,fixtureMasks=root.childCount-2},true);
    }
    public static object Track(int index)
    {
        var game=Game();Call(game,"SelectPlaylist",index);Refresh();var graphic=(SimBarPixelPolygonGraphic)Get(game,"pixelAlbumImage");var texture=graphic.mainTexture;var points=(Vector2[])Get(graphic,"points");var uv=(Vector2[])Get(graphic,"textureCoordinates");
        string expected=index==1?"julywantsmore-hey":index==2?"julywantsmore-no-matter-how":"freewill-electric-shocker";
        Require(texture.name=="SimBarAlbumJackets/"+expected&&texture.width==224&&texture.height==224&&texture.filterMode==FilterMode.Bilinear,"Selected track jacket is wrong");
        float ratio=Vector2.Distance(points[0],points[1])/Vector2.Distance(points[0],points[3]);Require(Mathf.Abs(ratio-1f)<.00001f,"Square jacket is stretched");Require(uv.SequenceEqual(new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(1,0),new Vector2(0,0)}),"Jacket source is cropped");
        var physical=(Mesh)Get(game,"physicalAlbumImageMesh");var vertices=physical.vertices;Require(Mathf.Abs(Vector3.Distance(vertices[0],vertices[1])/Vector3.Distance(vertices[0],vertices[3])-1f)<.00001f,"Physical jacket is stretched");
        return Record("track-"+index,new{index,resource=texture.name,width=texture.width,height=texture.height,projectedAspect=ratio,fullSourceUV=true,physicalAspect=1,textureFilter=texture.filterMode.ToString()},index<3);
    }
    public static object Track0()=>Track(0);public static object Track1()=>Track(1);public static object Track2()=>Track(2);public static object Track3()=>Track(3);public static object Track4()=>Track(4);
    public static object FitFixtures()
    {
        var cases=new List<object>();foreach(Vector2 size in new[]{new Vector2(224,224),new Vector2(320,180),new Vector2(180,320)})
        {
            var points=(Vector2[])Call(Game(),"AlbumLedFit",size.x,size.y);float w=Vector2.Distance(points[0],points[1]),h=Vector2.Distance(points[0],points[3]);Require(Mathf.Abs(w/h-size.x/size.y)<.00001f,"Non-square fit changed aspect");
            Require(w<=new Vector2(177,46).magnitude+.001f&&h<=67.001f,"Fit crops source");cases.Add(new{width=size.x,height=size.y,displayWidth=w,displayHeight=h,ratio=w/h});
        }
        return Record("aspect-fixtures",new{cases});
    }
    public static object Pause()
    {
        var game=Game();Set(game,"paused",true);Call(game,"TogglePlaylistMusic");if(!(bool)Get(game,"musicPaused"))Call(game,"TogglePlaylistMusic");Refresh();
        var before=((Graphic)Get(game,"pixelAlbumImage")).mainTexture;for(int i=0;i<8;i++){Call(game,"TickVenueMotion",.1f);Call(game,"LateUpdate");}
        Require(before==((Graphic)Get(game,"pixelAlbumImage")).mainTexture&&Spots().All(l=>l.enabled),"Paused track lost jacket or lights");
        return Record("paused-cover",new{sameCover=true,musicPaused=Get(game,"musicPaused"),performanceLightsFrozenVisible=true});
    }
    static void ClosingLights()
    {
        Require(!Prop("StagePerformanceLightingActive")&&Spots().All(l=>!l.enabled&&l.intensity==0f)&&Beams().All(r=>!r.enabled)&&!((Transform)Get(Game(),"pixelLightRoot")).gameObject.activeSelf,"Closing relit stage");
        var expected=JObject.Parse(File.ReadAllText("Library/SimBarQA/StageScreen-ambient.json"));Require((string)expected["ambient"]==RenderSettings.ambientLight.ToString("F6"),"Venue ambient changed");
        Require(JToken.DeepEquals(expected["practicals"],JToken.FromObject(Practicals().Select(l=>new{l.name,l.enabled,l.intensity}).ToArray())),"Warm venue practicals changed");
    }
    public static object Closing()
    {
        var game=Game();Set(game,"paused",false);Set(Journey(),"secondsLeft",0f);Call(game,"FinishJourneyNight");Refresh();ClosingLights();
        Require((bool)Get(game,"musicPaused")&&(bool)Get(Journey(),"musicPaused")&&!(bool)Get(game,"paused"),"End did not pause only music");
        Require(!Resources.FindObjectsOfTypeAll<Canvas>().Any(c=>c.name.Contains("Closing Music")),"Extra left music canvas still exists");Require(!Resources.FindObjectsOfTypeAll<Button>().Any(b=>b.name=="Closing Music Play Pause"),"Extra left music button still exists");
        object floor=Get(Journey(),"floorMissions"),reaction=Get(floor,"reaction");Set(floor,"litter",32);Set(floor,"cleanupActive",true);Set(reaction,"activeKind",SimBarFloorChallenges.Cleanup);Set(reaction,"secondsLeft",7f);Refresh();
        var card=(RectTransform)Get(game,"floorReactionCard");Require(card.gameObject.activeInHierarchy&&Mathf.Abs(card.anchoredPosition.x-12f)<.1f,"Closing reaction was not moved left");var hits=Hits(Play());
        ClickPlay();Require(!(bool)Get(game,"musicPaused")&&!(bool)Get(Journey(),"musicPaused")&&!(bool)Get(game,"paused"),"Existing playlist did not manually resume during reaction");
        Call(game,"TryBeginFloorClosing");Refresh();Require(!(bool)Get(game,"musicPaused"),"Repeated closing re-paused manual playback");ClosingLights();game.enabled=false;
        return Record("closing-reaction-playlist",new{stageSpotsOff=true,physicalBeamsOff=true,pixelConesPoolsFlaresOff=true,ambientAndPracticalsUnchanged=true,onlyMusicAutoPaused=true,manualExistingPlaylistResume=true,reactionLeft=true,duplicateLeftMusicControl=false,raycast=hits,coverRetained=((Graphic)Get(game,"pixelAlbumImage")).mainTexture.name},true);
    }
    public static object ClosingTrack()
    {
        var game=Game();Call(game,"SelectPlaylist",1);Refresh();ClosingLights();Require(!(bool)Get(game,"musicPaused"),"Manual selected track was paused");
        object reaction=Get(Get(Journey(),"floorMissions"),"reaction");Set(reaction,"activeKind",0);Set(reaction,"cooldown",0f);Call(game,"OpenFloorMissionDrawer");Refresh();var hits=Hits(Play());
        Require(((RectTransform)Get(game,"floorMissionDrawer")).gameObject.activeInHierarchy,"Status drawer did not open");
        return Record("closing-status-playlist",new{selectedTrack=1,stageStillOff=true,existingPlaylistClickable=true,drawerLeft=true,raycast=hits},true);
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/StageScreen-GameView.json"));type.GetProperty("selectedSizeIndex",B).SetValue(window,(int)viewport["index"]);return Readback();
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/StageScreen-GameView.json"));
        var result=new{prefsRestored=exact,gameViewRestored=(int)type.GetProperty("selectedSizeIndex",B).GetValue(window)==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling};File.WriteAllText(Dir+"preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
