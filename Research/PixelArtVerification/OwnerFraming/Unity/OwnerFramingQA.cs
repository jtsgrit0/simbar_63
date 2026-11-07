using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class OwnerFramingQA
{
    const string Dir="Research/PixelArtVerification/OwnerFraming/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static object Call(object target,string key,params object[] args){try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();return null;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static Type ViewType()=>typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
    static object View()=>Resources.FindObjectsOfTypeAll(ViewType()).First();
    static object Record(string name,object value,bool capture=false){Directory.CreateDirectory(Dir);Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"),2);return value;}
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText("Library/SimBarQA/OwnerFraming-GameView.json",JsonConvert.SerializeObject(new{index=ViewType().GetProperty("selectedSizeIndex",B).GetValue(View())}));ViewType().GetProperty("selectedSizeIndex",B).SetValue(View(),1);return new{saved=true};
    }
    public static object Viewports()
    {
        var sizesType=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",B|BindingFlags.FlattenHierarchy).GetValue(null);var group=Call(sizes,"GetGroup",ViewType().GetProperty("currentSizeGroupType",B).GetValue(View()));var texts=(string[])Call(group,"GetDisplayTexts");
        int portrait=Array.FindIndex(texts,t=>t.Contains("9:16")||t.IndexOf("portrait",StringComparison.OrdinalIgnoreCase)>=0);
        return Record("viewport-availability",new{sizes=texts,portraitPresetIndex=portrait,portraitAvailable=portrait>=0,customPresetsAdded=false});
    }
    static object Choose(int index,string name=null)
    {
        var game=Game();var card=((Button[])Get(game,"ownerCards"))[index];Require(card.gameObject.activeInHierarchy&&card.interactable,"Owner card is unavailable");
        ExecuteEvents.Execute(card.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        var ids=new[]{"female","ff","pet-sounds"};Require(game.SelectedOwnerIndex==index&&PlayerPrefs.GetString(SimBarExperience.OwnerPreferenceKey)==ids[index],"Owner choice did not persist");Call(game,"LoadOwnerSelection");Call(game,"RefreshOwnerSelection");Require(game.SelectedOwnerId==ids[index],"Owner identity did not reload");
        var portraits=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Portrait "+i).GetComponent<RawImage>()).ToArray();
        foreach(var p in portraits)Require(p.texture!=null&&p.texture.width==400&&p.texture.height==350&&p.texture.filterMode==FilterMode.Point&&p.uvRect==new Rect(0f,0f,1f,1f),"Owner source was resized, filtered or cropped in the UI");
        var badges=(Text[])Get(game,"ownerBadges");
        for(int i=0;i<3;i++)
        {
            var rect=(RectTransform)badges[i].transform.parent;Require(rect.gameObject.activeSelf&&rect.anchoredPosition==new Vector2(-139f,193f)&&rect.sizeDelta==new Vector2(104f,30f),"Owner badge does not use the shared layout");
            Require(badges[i].fontSize==15&&badges[i].rectTransform.sizeDelta==new Vector2(98f,28f),"Owner badge label geometry differs");
            if(i!=index)Require(badges[i].text=="0"+(i+1),"Unselected owner number missing");
        }
        return Record(name??("selected-"+ids[index]),new{selectedOwner=game.SelectedOwnerId,persistedAndReloaded=true,sharedBadges=true,viewport=new{width=Screen.width,height=Screen.height},portraits=portraits.Select(p=>new{name=p.name,texture=p.texture.name,width=p.texture.width,height=p.texture.height,drawWidth=p.rectTransform.sizeDelta.x,drawHeight=p.rectTransform.sizeDelta.y,filter=p.texture.filterMode.ToString(),uv=p.uvRect.ToString("F6")}).ToArray()},true);
    }
    public static object Jessy(){SimBarLocalization.SetLanguage(true);Call(Game(),"RefreshOwnerSelection");return Choose(0);}
    public static object Edison()=>Choose(1);
    public static object Jaiden()=>Choose(2);
    public static object English()
    {
        SimBarLocalization.SetLanguage(false);Call(Game(),"RefreshOwnerSelection");var names=((Text[])Get(Game(),"ownerNames")).Select(t=>t.text).ToArray();var descriptions=((Text[])Get(Game(),"ownerDescriptions")).Select(t=>t.text).ToArray();
        Require(names.SequenceEqual(new[]{"JESSY","EDISON","JAIDEN"})&&descriptions.SequenceEqual(new[]{"Darling, come play live tonight.","Rock&Roll for life.","What shall we listen to tonight?"}),"Owner names/descriptions changed");return Record("owners-english",new{names,descriptions},true);
    }
    public static object Portrait()
    {
        object info=Viewports();int index=(int)info.GetType().GetProperty("portraitPresetIndex").GetValue(info);if(index<0)return new{tested=false,reason="No existing portrait GameView preset; added no persistent viewport settings."};
        ViewType().GetProperty("selectedSizeIndex",B).SetValue(View(),index);return new{tested=true,index};
    }
    public static object PortraitCapture()=>Choose(0,"owners-portrait");
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/OwnerFraming-GameView.json"));ViewType().GetProperty("selectedSizeIndex",B).SetValue(View(),(int)viewport["index"]);return Readback();
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/OwnerFraming-GameView.json"));
        var result=new{prefsRestored=exact,gameViewRestored=(int)ViewType().GetProperty("selectedSizeIndex",B).GetValue(View())==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling};File.WriteAllText(Dir+"preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
