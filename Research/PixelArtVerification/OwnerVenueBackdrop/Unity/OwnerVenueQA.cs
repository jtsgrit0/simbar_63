using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class OwnerVenueQA
{
    const string Dir="Research/PixelArtVerification/OwnerVenueBackdrop/Unity/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);
        var saved=new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)});
        File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(saved));return new{saved=true};
    }
    public static object Inspect()
    {
        if(!Application.isPlaying)throw new Exception("Expected Play Mode");
        var game=UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
        var room=GameObject.Find("Owner Title Fullscreen Venue").GetComponent<RawImage>();
        var city=GameObject.Find("Owner Title Fullscreen Seoul Night").GetComponent<RawImage>();
        var card=GameObject.Find("Owner Title Venue Backdrop").GetComponent<RawImage>();
        Canvas.ForceUpdateCanvases();var corners=new Vector3[4];var viewport=new Vector3[4];room.rectTransform.GetWorldCorners(corners);((RectTransform)room.transform.parent).GetWorldCorners(viewport);
        var portraits=Enumerable.Range(0,3).Select(i=>GameObject.Find("Journey Owner Portrait "+i).GetComponent<RawImage>()).Select(i=>new{name=i.name,width=i.texture.width,height=i.texture.height,drawWidth=i.rectTransform.sizeDelta.x,drawHeight=i.rectTransform.sizeDelta.y}).ToArray();
        var result=new{venue=room.texture.name,width=room.texture.width,height=room.texture.height,pointFilter=room.texture.filterMode==FilterMode.Point,cover=room.GetComponent<AspectRatioFitter>().aspectMode==AspectRatioFitter.AspectMode.EnvelopeParent,coversViewport=corners[0].x<=viewport[0].x+.1f&&corners[0].y<=viewport[0].y+.1f&&corners[2].x>=viewport[2].x-.1f&&corners[2].y>=viewport[2].y-.1f,cityBelowVenue=city.transform.GetSiblingIndex()<room.transform.GetSiblingIndex(),sameCardVenue=card.texture==room.texture,raycastPasses=!room.raycastTarget,selectedOwner=game.SelectedOwnerId,portraits};
        File.WriteAllText(Dir+"visual-contract.json",JsonConvert.SerializeObject(result,Formatting.Indented));ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"owner-selector.png"));return result;
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var result=new{prefsRestored=exact,scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground};File.WriteAllText(Dir+"preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
