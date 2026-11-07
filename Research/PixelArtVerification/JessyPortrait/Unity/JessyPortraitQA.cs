using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class JessyPortraitQA
{
    const string Dir="Research/PixelArtVerification/JessyPortrait/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static object Call(object target,string key,params object[] args){try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static object Record(string name,object value,bool capture=false){Directory.CreateDirectory(Dir);Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"),2);return value;}
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText("Library/SimBarQA/JessyPortrait-GameView.json",JsonConvert.SerializeObject(new{index=type.GetProperty("selectedSizeIndex",B).GetValue(window)}));type.GetProperty("selectedSizeIndex",B).SetValue(window,1);return new{saved=true};
    }
    static object Choose(int index)
    {
        var game=Game();var card=((Button[])Get(game,"ownerCards"))[index];Require(card.gameObject.activeInHierarchy&&card.interactable,"Owner card is not available");
        ExecuteEvents.Execute(card.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        var ids=new[]{"female","ff","pet-sounds"};Require(game.SelectedOwnerIndex==index&&game.SelectedOwnerId==ids[index]&&PlayerPrefs.GetString(SimBarExperience.OwnerPreferenceKey)==ids[index],"Owner selection did not persist");
        Call(game,"LoadOwnerSelection");Call(game,"RefreshOwnerSelection");Require(game.SelectedOwnerIndex==index,"Owner identity did not reload");
        var portraits=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Portrait "+i).GetComponent<RawImage>()).ToArray();
        foreach(var portrait in portraits)Require(portrait.texture!=null&&portrait.uvRect==new Rect(0f,0f,1f,1f),"Owner portrait source is unexpectedly cropped");
        var badges=(Text[])Get(game,"ownerBadges");var jessyBadge=(RectTransform)badges[0].transform.parent;
        Require(jessyBadge.gameObject.activeSelf==(index==0)&&jessyBadge.anchoredPosition==new Vector2(-167f,196f)&&jessyBadge.sizeDelta==new Vector2(60f,24f),"Jessy's selected-only side-bar badge is wrong");
        Require(badges[0].fontSize==11&&badges[0].rectTransform.sizeDelta==new Vector2(56f,20f),"Jessy's badge label changed the framing contract");
        Require(badges.Skip(1).All(b=>b.transform.parent.gameObject.activeSelf&&((RectTransform)b.transform.parent).sizeDelta==new Vector2(104f,30f)),"Other owner badges changed");
        return Record("selected-"+ids[index],new{selectedOwner=game.SelectedOwnerId,persisted=true,reloaded=true,jessySelectedOnlyBadge=(index==0),otherBadgesUnchanged=true,portraits=portraits.Select(p=>new{name=p.name,texture=p.texture.name,width=p.texture.width,height=p.texture.height,drawWidth=p.rectTransform.sizeDelta.x,drawHeight=p.rectTransform.sizeDelta.y,uv=p.uvRect.ToString("F6")}).ToArray()},true);
    }
    public static object Jessy(){SimBarLocalization.SetLanguage(true);Call(Game(),"RefreshOwnerSelection");return Choose(0);}
    public static object Edison()=>Choose(1);
    public static object Jaiden()=>Choose(2);
    public static object English()
    {
        SimBarLocalization.SetLanguage(false);Call(Game(),"RefreshOwnerSelection");var names=((Text[])Get(Game(),"ownerNames")).Select(t=>t.text).ToArray();var descriptions=((Text[])Get(Game(),"ownerDescriptions")).Select(t=>t.text).ToArray();
        Require(names.SequenceEqual(new[]{"JESSY","EDISON","JAIDEN"}),"Owner names changed");Require(descriptions.SequenceEqual(new[]{"Darling, come play live tonight.","Rock&Roll for life.","What shall we listen to tonight?"}),"Owner descriptions changed");
        return Record("owners-english",new{names,descriptions},true);
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/JessyPortrait-GameView.json"));type.GetProperty("selectedSizeIndex",B).SetValue(window,(int)viewport["index"]);return Readback();
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/JessyPortrait-GameView.json"));
        var result=new{prefsRestored=exact,gameViewRestored=(int)type.GetProperty("selectedSizeIndex",B).GetValue(window)==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling};File.WriteAllText(Dir+"preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
