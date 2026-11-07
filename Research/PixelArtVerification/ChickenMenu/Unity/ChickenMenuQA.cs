using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ChickenMenuQA
{
    const string Dir="Research/PixelArtVerification/ChickenMenu/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object target,string key)=>target.GetType().GetField(key,B).GetValue(target);
    static void Set(object target,string key,object value)=>target.GetType().GetField(key,B).SetValue(target,value);
    static object Call(object target,string key,params object[] args){try{return target.GetType().GetMethod(key,B).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static object Journey()=>Get(Game(),"journey");
    static object Queue()=>Get(Game(),"orderQueue");
    static object Patron()=>((IList)Get(Game(),"patrons"))[8];
    static object Actor()=>((IDictionary)Get(Game(),"motionLookup"))[Patron()];
    static object View()=>((IDictionary)Get(Game(),"pixelActorViews"))[Actor()];
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static object Record(string name,object value,bool capture=false){Directory.CreateDirectory(Dir);Call(Game(),"LateUpdate");Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+name+".png"),2);return value;}
    static void ClearOrders(){foreach(object patron in (IList)Get(Game(),"patrons"))Call(Game(),"ClearOrder",patron);Call(Queue(),"Clear");}
    static void Chicken(){Call(Game(),"CancelPourGame",false);ClearOrders();Call(Game(),"AssignOrder",Patron(),5);Set(Patron(),"patience",30f);Call(Game(),"RefreshUI");Call(Game(),"LateUpdate");}
    static void Fresh()
    {
        var game=Game();Set(Journey(),"inProgress",false);Set(Journey(),"tutorialComplete",true);Call(game,"StartFreshJourneyNight",false);Set(game,"paused",true);SimBarLocalization.SetLanguage(true);
        Array.Clear((Array)Get(game,"opUpgrades"),0,3);Set(game,"opActiveEvent",-1);Set(Journey(),"feverSeconds",0f);Set(game,"opAutoServeTimer",12f);Call(game,"LateUpdate");
    }
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText("Library/SimBarQA/ChickenMenu-GameView.json",JsonConvert.SerializeObject(new{index=type.GetProperty("selectedSizeIndex",B).GetValue(window)}));type.GetProperty("selectedSizeIndex",B).SetValue(window,1);return new{saved=true};
    }
    public static object Verifier()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("SimBarServiceMenuVerification")).First(t=>t!=null);return JObject.Parse((string)type.GetMethod("Verify",B).Invoke(null,null));
    }
    public static object RandomAndBubble()
    {
        Fresh();var game=Game();UnityEngine.Random.InitState(3491);var counts=new int[6];
        for(int i=0;i<96;i++){ClearOrders();Call(game,"TryCreateOrder",(object)null);object patron=Call(Queue(),"Peek");int item=(int)Get(patron,"drinkIndex");Require(item>=0&&item<6,"Generated invalid item");counts[item]++;}
        Require(counts.All(count=>count>0),"Random orders did not cover chicken and all old items");Chicken();var view=View();var icon=(RawImage)Get(view,"orderIcon");var bubble=(RectTransform)Get(view,"orderBubble");
        Require(bubble.gameObject.activeSelf&&Mathf.Abs(icon.uvRect.x-5f/6f)<.000001f,"Chicken bubble does not use atlas tile 5");var colors=(Color[])typeof(SimBarExperience).GetField("DrinkColors",B).GetValue(null);Require(colors.Take(5).All(c=>c!=colors[5]),"Chicken marker color is not distinct");
        return Record("chicken-order-bubble",new{randomCounts=counts,chickenItem=5,iconCell=5,uv=icon.uvRect.ToString("F6"),distinctMarker=true},true);
    }
    static object Menu(bool korean)
    {
        var game=Game();SimBarLocalization.SetLanguage(korean);Set(game,"paused",false);Call(game,"OpenServiceMenu");Canvas.ForceUpdateCanvases();var root=(GameObject)Get(game,"serviceMenuRoot");var scroll=root.GetComponentInChildren<ScrollRect>(true);scroll.verticalNormalizedPosition=0f;Canvas.ForceUpdateCanvases();
        var button=((Button[])Get(game,"serviceItemButtons"))[5];var corners=new Vector3[4];var viewport=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);scroll.viewport.GetWorldCorners(viewport);
        Require(corners[0].y>=viewport[0].y-.6f&&corners[2].y<=viewport[2].y+.6f,"Fourth food row cannot scroll into view");
        Require(root.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("Service Menu Item "))==6,"Menu is not six items");
        string title=button.GetComponentInChildren<Text>().text;Require(title.Contains(korean?"후라이드 치킨":"FRIED CHICKEN"),"Chicken menu title missing");
        Require(root.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("₩18,000")),"Chicken price does not display 18000 won");
        return Record(korean?"menu-korean-scrolled":"menu-english-scrolled",new{items=6,foodRows=4,contentHeight=scroll.content.rect.height,viewportHeight=scroll.viewport.rect.height,chickenVisible=true,title,price="₩18,000",hints=root.GetComponentsInChildren<Text>(true).First(t=>t.name=="Service Menu Hint").text},true);
    }
    public static object KoreanMenu()=>Menu(true);
    public static object EnglishMenu()=>Menu(false);
    public static object PrepareDigit6()
    {
        var game=Game();if(!(bool)game.GetType().GetProperty("JourneyIsPlaying",B).GetValue(game))Fresh();Call(game,"CloseServiceMenu");SimBarLocalization.SetLanguage(true);Chicken();var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");((UnityEditor.EditorWindow)Resources.FindObjectsOfTypeAll(type).First()).Focus();Set(game,"paused",false);Require(Keyboard.current!=null,"Keyboard device unavailable");
        UnityEditor.EditorApplication.CallbackFunction freeze=null;freeze=()=>{if(game==null){UnityEditor.EditorApplication.update-=freeze;return;}if((bool)Get(game,"pourRunning")){game.enabled=false;UnityEditor.EditorApplication.update-=freeze;}};UnityEditor.EditorApplication.update+=freeze;
        return new{ready=true,phase=Get(game,"journeyPhase").ToString(),paused=Get(game,"paused"),focused=UnityEditor.EditorWindow.focusedWindow.titleContent.text};
    }
    public static object Digit6Plating()
    {
        var game=Game();if(!(bool)game.GetType().GetProperty("JourneyIsPlaying",B).GetValue(game))Fresh();
        bool keyVerified=(bool)Get(game,"pourRunning")&&(int)Get(game,"pourDrinkIndex")==5;
        if(!keyVerified)Record("digit6-diagnostics",new{paused=Get(game,"paused"),phase=Get(game,"journeyPhase").ToString(),enabled=game.enabled,keyboard=Keyboard.current?.name,focused=UnityEditor.EditorWindow.focusedWindow?.titleContent.text,inputMode=InputSystem.settings.updateMode.ToString(),front=(int)Get(Call(Queue(),"Peek"),"drinkIndex"),actionFrame=Get(game,"pourActionFrame"),frame=Time.frameCount,guard=Get(game,"pourInputGuardUntil"),now=Time.unscaledTime});
        game.enabled=true;Chicken();Set(game,"paused",false);Call(game,"OpenServiceMenu");((Button[])Get(game,"serviceItemButtons"))[5].onClick.Invoke();
        Require((bool)Get(game,"pourRunning")&&(int)Get(game,"pourDrinkIndex")==5,"Chicken menu button did not start plating");Require(((Text)Get(game,"pourTitle")).text.Contains("플레이팅"),"Chicken used a drink pour instead of food plating");game.enabled=false;
        return Record("chicken-food-plating",new{item=5,keyboardDigit=6,key6Verified=keyVerified,menuButtonVerified=true,food=true,title=((Text)Get(game,"pourTitle")).text},true);
    }
    public static object MenuPlating()=>Digit6Plating();
    public static object ManualPerfect()
    {
        var game=Game();game.enabled=true;Set(game,"paused",false);Set(game,"cash",1000f);Set(Patron(),"patience",30f);Set(game,"pourCursorPosition",(float)Get(game,"pourTargetCenter"));Call(game,"StopPlayerPour");
        Require(!(bool)Get(game,"pourRunning")&&(float)Get(game,"cash")==1029f&&(int)Get(Patron(),"drinkIndex")==-1,"Manual chicken sale/tip/order clear failed");Call(game,"StopPlayerPour");Require((float)Get(game,"cash")==1029f,"Repeated stop paid twice");Set(game,"paused",true);
        return Record("manual-chicken",new{price=18,perfectTip=11,income=29,cash=Get(game,"cash"),servedOnce=true},true);
    }
    public static object Automatic()
    {
        var game=Game();Chicken();Set(game,"paused",false);Set(game,"cash",1000f);Call(game,"ServeDrink",5);Require((float)Get(game,"cash")==1026f&&(int)Get(Patron(),"drinkIndex")==-1,"Automatic chicken sale/tip/order clear failed");Set(game,"paused",true);
        return Record("automatic-chicken",new{price=18,automaticTip=8,income=26,cash=Get(game,"cash")});
    }
    public static object SaveCompatibility()
    {
        var game=Game();Chicken();Call(game,"SaveOperations");string saved=PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey);var data=JObject.Parse(saved);float cash=(float)Get(game,"cash");Call(game,"LoadOperations");Call(game,"ContinueJourney");Set(game,"paused",true);
        var queue=(IEnumerable)Queue();var regenerated=queue.Cast<object>().Select(p=>(int)Get(p,"drinkIndex")).ToArray();Require(regenerated.Length>0&&regenerated.All(i=>i>=0&&i<6)&&(float)Get(game,"cash")==cash,"Save/Continue regenerated invalid orders or changed cash");
        var backup=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json")).First(i=>(string)i["key"]==SimBarExperience.OperationsSaveKey);bool oldLoaded=true;
        if((bool)backup["exists"]){var old=JObject.Parse((string)backup["value"]);PlayerPrefs.SetString(SimBarExperience.OperationsSaveKey,(string)backup["value"]);Call(game,"LoadOperations");oldLoaded=(float)Get(game,"cash")== (float)old["money"];PlayerPrefs.SetString(SimBarExperience.OperationsSaveKey,saved);Call(game,"LoadOperations");Call(game,"ContinueJourney");Set(game,"paused",true);}
        Require(oldLoaded,"Existing five-item save no longer loads");return Record("save-compatibility",new{operationsVersion=(int)data["version"],oldSaveLoaded=oldLoaded,queueRemainsTransient=true,regeneratedItems=regenerated,allBoundsUseSixItems=true});
    }
    public static object ClosingClear()
    {
        var game=Game();Chicken();Set(game,"paused",false);Set(Journey(),"secondsLeft",0f);Call(game,"FinishJourneyNight");Set(game,"paused",true);Call(game,"LateUpdate");bool hidden=!((RectTransform)Get(View(),"orderBubble")).gameObject.activeSelf;Require((int)Queue().GetType().GetProperty("Count").GetValue(Queue())==0&&hidden,"Closing did not clear chicken order/bubble");return Record("closing-clear",new{orderCleared=true,bubbleHidden=hidden});
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var item in saved){string key=(string)item["key"];if((bool)item["exists"])PlayerPrefs.SetString(key,(string)item["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/ChickenMenu-GameView.json"));type.GetProperty("selectedSizeIndex",B).SetValue(window,(int)viewport["index"]);return Readback();
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));bool exact=saved.All(item=>PlayerPrefs.HasKey((string)item["key"])==(bool)item["exists"]&&PlayerPrefs.GetString((string)item["key"])==(string)item["value"]);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=Resources.FindObjectsOfTypeAll(type).First();var viewport=JObject.Parse(File.ReadAllText("Library/SimBarQA/ChickenMenu-GameView.json"));
        Directory.CreateDirectory(Dir);var result=new{prefsRestored=exact,gameViewRestored=(int)type.GetProperty("selectedSizeIndex",B).GetValue(window)==(int)viewport["index"],scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling};File.WriteAllText(Dir+"preservation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
