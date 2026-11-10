using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The approved room plate and pixel atlas present the existing simulated people.
/// This layer never moves a person, serves an order, advances missions or alters saves.
/// The separate overlay Canvas leaves the original HUD and proprietor UI at native resolution.
/// </summary>
public sealed partial class SimBarExperience
{
    [Serializable] private sealed class PixelDirection{public string id;public int column;}
    [Serializable] private sealed class PixelStyle{public string id;public int row;}
    [Serializable] private sealed class PixelAtlas
    {
        public string id,resource;public int frameWidth,frameHeight,columns,cropHeight,drawHeight,footX,footY;public PixelDirection[] directionColumns;
    }
    [Serializable] private sealed class PixelAction
    {
        public string id,atlas,direction;public string[] atlasFrames;public int row,drawHeight,cropHeight,footX,footY;public bool hasRow,horizontal,preserveStyle;public int[] frameColumns,variantRows;
        public float fps;public int frameCount;public bool progressFrames;public PixelStyle[] variantStyles,directionRows,directionRowOffsets;public PixelDirection[] directionColumns;
    }
    [Serializable] private sealed class PixelRole
    {
        public string id,atlas,direction;public string[] styles,atlasFrames;public int row,drawHeight,cropHeight,footX,footY;public bool horizontal;public int[] variantRows,frameColumns;
        public float fps;public PixelAction[] actions;public PixelStyle[] variantStyles,directionRows,directionRowOffsets;public PixelDirection[] directionColumns;
    }
    [Serializable] private sealed class PixelOffset{public string id;public Vector2 offset;}
    [Serializable] private sealed class PixelBar{public float xMax,xFade,zMin,zMax,xMin,zFade;public bool boundedX;public Vector2 offset;}
    [Serializable] private sealed class PixelOccluder{public Vector2[] points;public float depth;public string[] actors;}
    [Serializable] private sealed class PixelSeat{public string id;public Vector2 feet;public float depth;public bool hideShadow;}
    [Serializable] private sealed class PixelProp{public string resource;public float x,z,drawWidth,drawHeight,footX,footY,depthOffset;}
    [Serializable] private sealed class PixelManifest
    {
        public int frameWidth=64,frameHeight=64,columns=4,drawHeight=54,footX=32,footY=60;
        public string defaultAtlas;
        public Vector2[] ledQuad,ledLamps,stageDeck,stageLamps,lightSources;
        public PixelOffset[] actorOffsets,bandStageFeet;public float stageElevationPixels,drumForegroundDepth,ledFixtureScale;public PixelBar barRegion;public PixelOccluder[] occluders;public PixelSeat[] seatedActors;public PixelRole[] roles;public PixelAtlas[] atlases;public PixelProp[] props;
    }
    private sealed class PixelActorView
    {
        public MotionActor actor;public RectTransform root;public RawImage image;public Image marker,broom,bristles;public SimBarPixelPolygonGraphic shadow;public string id,role,originalRole;public PixelLayer layer;
        public RectTransform orderBubble;public RawImage orderIcon;
        public Vector2 orderBase;public SimBarPixelPolygonGraphic orderTail,orderTailFill;
        public uint variantSeed;public PixelSeat seat;
    }
    private sealed class PixelLayer{public Transform root;public float depth;public List<Transform> after;}
    private sealed class PixelPropView{public PixelProp definition;public Texture2D texture;public float depth;}
    private sealed class PixelVenueMask{public SimBarPixelPolygonGraphic graphic;public Vector2[] points;}
    private sealed class PixelParticle{public Vector3 position,velocity;public float age,phase;public bool tissue;public int index,sequence;public SimBarPixelPaperGraphic image;}
    private struct PixelPaperRandom
    {
        private uint value;
        public PixelPaperRandom(uint seed){value=seed;}
        public float Next(){unchecked{value=value*1664525u+1013904223u;}return (float)(value/4294967296d);}
    }
    private Canvas pixelWorldCanvas;
    private RectTransform pixelRoom;
    private RawImage pixelVenuePlate;
    private Texture2D pixelVenueTexture;
    private int pixelVenueOwner=-1;
    private readonly List<PixelVenueMask> pixelVenueMasks=new List<PixelVenueMask>();
    private SimBarMiniMap pixelMiniMap;
    private RawImage pixelCity;
    private Texture2D pixelCharacters;
    private Camera pixelSceneCamera;
    private int pixelOriginalCullingMask;
    private bool pixelCameraOwned;
    private PixelManifest pixelManifest;
    private PixelManifest pixelOriginalManifest,pixelFFManifest,pixelFreebirdManifest;
    private readonly Dictionary<MotionActor,PixelActorView> pixelActorViews=new Dictionary<MotionActor,PixelActorView>();
    private readonly List<PixelLayer> pixelStaticLayers=new List<PixelLayer>();
    private readonly List<PixelPropView> pixelPropViews=new List<PixelPropView>();
    private readonly List<PixelLayer> pixelDrawLayers=new List<PixelLayer>(64);
    private readonly List<MotionActor> pixelRemovedActors=new List<MotionActor>();
    private readonly List<SimBarPixelPaperGraphic> pixelPaper=new List<SimBarPixelPaperGraphic>(104);
    private readonly List<Vector2> pixelPaperPositions=new List<Vector2>(104);
    private readonly List<PixelParticle> pixelParticles=new List<PixelParticle>(160);
    private int pixelPaperSequence;
    private RectTransform pixelAirborne;
    private RectTransform pixelOrderBubbles;
    private readonly List<Rect> pixelOrderCards=new List<Rect>();
    private static readonly Vector2[] PixelOrderDodge={Vector2.zero,new Vector2(-36f,0f),new Vector2(36f,0f),new Vector2(-72f,0f),new Vector2(72f,0f),new Vector2(0f,-34f),new Vector2(-36f,-34f),new Vector2(36f,-34f),new Vector2(0f,-68f)};
    private readonly Dictionary<string,PixelRole> pixelRoles=new Dictionary<string,PixelRole>();
    private readonly Dictionary<string,PixelAtlas> pixelAtlases=new Dictionary<string,PixelAtlas>();
    private readonly Dictionary<string,Texture2D> pixelAtlasTextures=new Dictionary<string,Texture2D>();
    private readonly Dictionary<string,Texture2D> pixelDecodedTextures=new Dictionary<string,Texture2D>();
    private int pixelScreenWidth,pixelScreenHeight;
    private int pixelConfettiSeen,pixelTissueSeen,pixelSession=-1;
    private SimBarFloorMissions.State pixelObservedFloorState;
    private float pixelAnimationSeconds;
    private static readonly Color32[] PixelPaperColors={new Color32(255,111,169,255),new Color32(98,226,237,255),new Color32(255,225,124,255),new Color32(185,160,255,255)};
    private static readonly int[] PixelIdleFrame={0};
    private static readonly Vector3 PixelEye=new Vector3(13f,21f,-25f),PixelLook=new Vector3(0f,1.45f,.15f);
    private static readonly Vector3 PixelForward=(PixelLook-PixelEye).normalized;
    private static readonly Vector3 PixelRight=Vector3.Cross(Vector3.up,PixelForward).normalized;
    private static readonly Vector3 PixelUp=Vector3.Cross(PixelForward,PixelRight);

    private void InitializePixelPresentation()
    {
        LoadOwnerSelection();
        Texture2D venue=LoadOwnerPixelVenue();
        pixelCharacters=LoadPixelTexture("SimBarPixelArt/Characters");
        Texture2D city=LoadPixelTexture("SimBarPixelArt/Seoul");
        TextAsset manifest=Resources.Load<TextAsset>("SimBarPixelArt/Presentation");
        if(venue==null||pixelCharacters==null||city==null||manifest==null)
        {Debug.LogWarning("Pixel room assets are incomplete; keeping the authored 3D presentation.");return;}
        try
        {
            pixelOriginalManifest=JsonUtility.FromJson<PixelManifest>(manifest.text);
            TextAsset ffManifest=Resources.Load<TextAsset>("SimBarPixelArt/PresentationFF");
            if(ffManifest!=null)pixelFFManifest=JsonUtility.FromJson<PixelManifest>(ffManifest.text);
            TextAsset freebirdManifest=Resources.Load<TextAsset>("SimBarPixelArt/PresentationFreebird");
            if(freebirdManifest!=null)pixelFreebirdManifest=JsonUtility.FromJson<PixelManifest>(freebirdManifest.text);
            pixelManifest=OwnerPixelManifest();
        }
        catch(Exception error){Debug.LogError(error);return;}
        if(pixelManifest?.roles==null||pixelManifest.roles.Length==0)return;
        venue.filterMode=pixelCharacters.filterMode=city.filterMode=FilterMode.Point;
        foreach(PixelRole role in pixelManifest.roles)pixelRoles[role.id]=role;
        foreach(PixelAtlas atlas in pixelManifest.atlases??Array.Empty<PixelAtlas>())
        {
            Texture2D texture=LoadPixelTexture(atlas.resource);
            if(texture==null){Debug.LogError("Missing approved pixel atlas: "+atlas.resource);return;}
            texture.filterMode=FilterMode.Point;pixelAtlases[atlas.id]=atlas;pixelAtlasTextures[atlas.id]=texture;
        }
        var canvasObject=new GameObject("SimBar Approved Pixel World",typeof(RectTransform),typeof(Canvas));
        canvasObject.transform.SetParent(transform,false);pixelWorldCanvas=canvasObject.GetComponent<Canvas>();
        pixelWorldCanvas.renderMode=RenderMode.ScreenSpaceOverlay;pixelWorldCanvas.sortingOrder=400;
        pixelWorldCanvas.pixelPerfect=true;
        pixelCity=PixelImage("Pixel Seoul Night",canvasObject.transform,city);
        pixelCity.rectTransform.anchorMin=Vector2.zero;pixelCity.rectTransform.anchorMax=Vector2.one;
        pixelCity.rectTransform.offsetMin=pixelCity.rectTransform.offsetMax=Vector2.zero;
        pixelRoom=new GameObject("Pixel Venue and Real Actors",typeof(RectTransform)).GetComponent<RectTransform>();
        pixelRoom.SetParent(canvasObject.transform,false);pixelRoom.anchorMin=pixelRoom.anchorMax=new Vector2(.5f,.5f);
        pixelRoom.pivot=new Vector2(.5f,.5f);pixelRoom.sizeDelta=new Vector2(960f,558f);
        pixelVenueTexture=venue;pixelVenueOwner=selectedOwner;
        pixelVenuePlate=PixelImage("Approved Empty Room Plate",pixelRoom,venue);PixelRect(pixelVenuePlate.rectTransform,Vector2.zero,new Vector2(960f,558f));
        InitializeAlbumJackets(venue);
        InitializePixelStageOff();
        InitializePixelLights();
        var paperRoot=new GameObject("Mission Paper from Saved State",typeof(RectTransform)).GetComponent<RectTransform>();
        paperRoot.SetParent(pixelRoom,false);PixelRect(paperRoot,Vector2.zero,new Vector2(960f,558f));
        var paperRandom=new PixelPaperRandom(44091);
        for(int i=0;i<104;i++)
        {
            var fleck=PixelPaperGraphic("Saved Paper "+i,paperRoot);pixelPaper.Add(fleck);
            pixelPaperPositions.Add(PixelProject(new Vector3(2.1f+paperRandom.Next()*7f,.03f,-4.6f+paperRandom.Next()*5.4f)));
        }
        pixelAirborne=new GameObject("Actual Performance Paper in Flight",typeof(RectTransform)).GetComponent<RectTransform>();
        pixelAirborne.SetParent(pixelRoom,false);PixelRect(pixelAirborne,Vector2.zero,new Vector2(960f,558f));
        pixelOrderBubbles=new GameObject("Actual Pending Order Bubbles",typeof(RectTransform)).GetComponent<RectTransform>();
        pixelOrderBubbles.SetParent(pixelRoom,false);PixelRect(pixelOrderBubbles,Vector2.zero,new Vector2(960f,558f));
        for(int i=0;i<160;i++){var particle=new PixelParticle{image=PixelPaperGraphic("Airborne Paper "+i,pixelAirborne)};particle.image.gameObject.SetActive(false);pixelParticles.Add(particle);}
        BuildPixelStaticLayers();
        // Keep all navigation transforms, lights and mission actors alive. The main
        // camera now contributes only the base clear; the separate minimap still works.
        pixelSceneCamera=Camera.main;TakePixelCamera();
        FitPixelPresentation();
    }
    private void BuildPixelStaticLayers()
    {
        pixelPropViews.Clear();
        foreach(PixelOccluder layer in pixelManifest.occluders??Array.Empty<PixelOccluder>())
        {
            if(layer.actors!=null&&layer.actors.Length>0)continue;
            var polygon=new GameObject("Pixel Furniture Foreground",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
            polygon.transform.SetParent(pixelRoom,false);RegisterPixelVenueMask(polygon,layer.points);
            var staticLayer=new PixelLayer{root=polygon.transform,depth=layer.depth};
            Transform unlit=CreatePixelStageOffOccluder(layer.points);
            if(unlit!=null)staticLayer.after=new List<Transform>{unlit};
            pixelStaticLayers.Add(staticLayer);
        }
        foreach(PixelProp prop in pixelManifest.props??Array.Empty<PixelProp>())
        {
            Texture2D texture=LoadPixelTexture(prop.resource);
            if(texture==null){Debug.LogWarning("Missing pixel venue prop: "+prop.resource);continue;}
            Vector2 point=PixelProject(new Vector3(prop.x,0f,prop.z));
            RawImage image=PixelImage("Pixel Venue Prop / "+prop.resource,pixelRoom,texture);
            PixelRect(image.rectTransform,new Vector2(Mathf.Floor(point.x-prop.footX+.5f),Mathf.Floor(point.y-prop.footY+.5f)),new Vector2(prop.drawWidth,prop.drawHeight));
            pixelStaticLayers.Add(new PixelLayer{root=image.transform,depth=point.y+prop.depthOffset});
            pixelPropViews.Add(new PixelPropView{definition=prop,texture=texture,depth=point.y+prop.depthOffset});
        }
        pixelPropViews.Sort((a,b)=>a.depth.CompareTo(b.depth));
    }
    private Texture2D LoadOwnerPixelVenue()
    {
        string resource=selectedOwner==0?"SimBarPixelArt/VenueFreebird":selectedOwner==1?"SimBarPixelArt/VenueFF":"SimBarPixelArt/Venue";
        return LoadPixelTexture(resource)??LoadPixelTexture("SimBarPixelArt/Venue");
    }
    private PixelManifest OwnerPixelManifest()=>selectedOwner==0&&pixelFreebirdManifest!=null?pixelFreebirdManifest:selectedOwner==1&&pixelFFManifest!=null?pixelFFManifest:pixelOriginalManifest;
    private void RegisterPixelVenueMask(SimBarPixelPolygonGraphic graphic,Vector2[] points)
    {
        graphic.Initialize(pixelVenueTexture,points);
        pixelVenueMasks.Add(new PixelVenueMask{graphic=graphic,points=points});
    }
    private void RefreshPixelVenue()
    {
        if(pixelVenueOwner==selectedOwner)return;
        Texture2D venue=LoadOwnerPixelVenue();if(venue==null)return;
        pixelVenueOwner=selectedOwner;
        PixelManifest nextManifest=OwnerPixelManifest();
        bool geometryChanged=!ReferenceEquals(nextManifest,pixelManifest);
        if(pixelVenueTexture==venue&&!geometryChanged)return;
        pixelVenueTexture=venue;pixelVenuePlate.texture=venue;
        if(geometryChanged)
        {
            foreach(PixelLayer layer in pixelStaticLayers)DestroyPixelFurnitureLayer(layer);
            pixelStaticLayers.Clear();pixelManifest=nextManifest;
            RefreshPixelAlbumGeometry();
            if(pixelStageOffRoot!=null){pixelStageOffRoot.gameObject.SetActive(false);Destroy(pixelStageOffRoot.gameObject);}
            InitializePixelStageOff();BuildPixelStaticLayers();
            foreach(PixelActorView view in pixelActorViews.Values)BindPixelActorFurniture(view);
        }
        for(int i=pixelVenueMasks.Count-1;i>=0;i--)
        {
            PixelVenueMask mask=pixelVenueMasks[i];
            if(mask.graphic==null){pixelVenueMasks.RemoveAt(i);continue;}
            mask.graphic.Initialize(venue,mask.points);
        }
    }
    private void DestroyPixelFurnitureLayer(PixelLayer layer)
    {
        if(layer.root!=null)
        {
            SimBarPixelPolygonGraphic graphic=layer.root.GetComponent<SimBarPixelPolygonGraphic>();
            pixelVenueMasks.RemoveAll(value=>value.graphic==graphic);
            layer.root.gameObject.SetActive(false);Destroy(layer.root.gameObject);
        }
        if(layer.after!=null)foreach(Transform mask in layer.after)
        {pixelStageOffOccluders.Remove(mask);mask.gameObject.SetActive(false);Destroy(mask.gameObject);}
    }
    private void BindPixelActorFurniture(PixelActorView view)
    {
        if(view.layer.after!=null)
        {
            foreach(Transform mask in view.layer.after)
            {
                SimBarPixelPolygonGraphic graphic=mask.GetComponent<SimBarPixelPolygonGraphic>();
                pixelVenueMasks.RemoveAll(value=>value.graphic==graphic);
                mask.gameObject.SetActive(false);Destroy(mask.gameObject);
            }
            view.layer.after.Clear();
        }
        view.role=IsStandingVenueLayout&&(view.originalRole=="seated"||view.originalRole=="barseated")?"guest":view.originalRole;
        view.seat=null;
        foreach(PixelSeat seat in pixelManifest.seatedActors??Array.Empty<PixelSeat>())if(seat.id==view.id){view.seat=seat;break;}
        foreach(PixelOccluder layer in pixelManifest.occluders??Array.Empty<PixelOccluder>())
        {
            if(layer.actors==null||Array.IndexOf(layer.actors,view.id)<0)continue;
            var mask=new GameObject("Pixel Seated Furniture / "+view.id,typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
            mask.transform.SetParent(pixelRoom,false);RegisterPixelVenueMask(mask,layer.points);
            if(view.layer.after==null)view.layer.after=new List<Transform>();view.layer.after.Add(mask.transform);
        }
    }
    private Texture2D LoadPixelTexture(string resource)
    {
        if(pixelDecodedTextures.TryGetValue(resource,out Texture2D cached))return cached;
        TextAsset encoded=Resources.Load<TextAsset>(resource);if(encoded==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false){name=resource,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        bool loaded=ImageConversion.LoadImage(texture,encoded.bytes,true);Resources.UnloadAsset(encoded);
        if(!loaded){Destroy(texture);return null;}
        texture.filterMode=FilterMode.Point;pixelDecodedTextures[resource]=texture;return texture;
    }
    private void OnEnable()
    {
        if(pixelWorldCanvas==null)return;pixelWorldCanvas.enabled=true;
        TakePixelCamera();
    }
    private void OnDisable()
    {
        if(pixelWorldCanvas!=null)pixelWorldCanvas.enabled=false;
        RestorePixelCamera();
    }
    private void TakePixelCamera()
    {
        if(pixelSceneCamera==null||pixelCameraOwned)return;
        pixelOriginalCullingMask=pixelSceneCamera.cullingMask;pixelSceneCamera.cullingMask=0;pixelCameraOwned=true;
    }
    private void RestorePixelCamera()
    {
        if(pixelSceneCamera!=null&&pixelCameraOwned)pixelSceneCamera.cullingMask=pixelOriginalCullingMask;
        pixelCameraOwned=false;
    }
    private void DestroyPixelPresentation()
    {
        DestroyPerformingBandPresentation();
        DestroyAlbumJackets();
        RestorePixelCamera();
        if(pixelWorldCanvas!=null)Destroy(pixelWorldCanvas.gameObject);
        pixelLightViews.Clear();pixelLightRoot=null;
        pixelStageOffOccluders.Clear();pixelStageOffRoot=null;
        foreach(Texture2D texture in pixelDecodedTextures.Values)if(texture!=null)Destroy(texture);
        pixelDecodedTextures.Clear();pixelAtlasTextures.Clear();pixelAtlases.Clear();
        pixelVenueMasks.Clear();pixelVenuePlate=null;pixelVenueTexture=null;pixelVenueOwner=-1;
        if(pixelMiniMap!=null)pixelMiniMap.BeginPixelOverview(null);pixelMiniMap=null;
        pixelActorViews.Clear();pixelStaticLayers.Clear();pixelDrawLayers.Clear();pixelParticles.Clear();pixelPaper.Clear();pixelPaperPositions.Clear();
        pixelPropViews.Clear();
    }

    private static RawImage PixelImage(string name,Transform parent,Texture image)
    {
        var result=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RawImage>();
        result.transform.SetParent(parent,false);result.texture=image;result.raycastTarget=false;return result;
    }
    private static Image PixelSolid(string name,Transform parent)
    {
        var result=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        result.transform.SetParent(parent,false);result.raycastTarget=false;return result;
    }
    private static SimBarPixelPaperGraphic PixelPaperGraphic(string name,Transform parent)
    {
        var result=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPaperGraphic)).GetComponent<SimBarPixelPaperGraphic>();
        result.transform.SetParent(parent,false);result.raycastTarget=false;return result;
    }
    private static void PixelRect(RectTransform rect,Vector2 point,Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(0f,1f);rect.pivot=new Vector2(0f,1f);
        rect.anchoredPosition=new Vector2(point.x,-point.y);rect.sizeDelta=size;
    }
    private static Vector2 PixelProject(Vector3 world)
    {
        Vector3 p=world-PixelEye;const float scale=558f/16.8f;
        return new Vector2(480f+Vector3.Dot(p,PixelRight)*scale,279f-Vector3.Dot(p,PixelUp)*scale);
    }
    private void FitPixelPresentation()
    {
        if(pixelWorldCanvas==null||pixelRoom==null)return;
        pixelScreenWidth=Screen.width;pixelScreenHeight=Screen.height;
        pixelRoom.localScale=Vector3.one*(Screen.height/558f);pixelRoom.anchoredPosition=Vector2.zero;
        float aspect=Screen.width/Mathf.Max(1f,Screen.height),imageAspect=pixelCity.texture.width/(float)pixelCity.texture.height;
        pixelCity.uvRect=imageAspect>aspect?new Rect((1f-aspect/imageAspect)*.5f,0f,aspect/imageAspect,1f)
            :new Rect(0f,(1f-imageAspect/aspect)*.5f,1f,imageAspect/aspect);
    }
    private PixelActorView CreatePixelActor(MotionActor actor)
    {
        string name=actor.person.name,id=name.ToLowerInvariant(),role="guest";
        if(name=="Singer"||name=="Guitarist"||name=="Bassist"||name=="Drummer")role=id;
        else if(name=="Bartender")role="bartender";else if(name=="Server")role="server";
        else if(name.StartsWith("Bar Guest ")){role="barseated";id="bar-seat-"+name.Substring(10);}
        else if(name.StartsWith("Lounge Guest ")){role="seated";id="lounge-"+name.Substring(13);}
        else if(actor.pitSlot==11){role="surfer";id="crowd-surfer";}
        else if(actor.pitSlot>=8&&actor.pitSlot<=10){role="support";id="support-"+(actor.pitSlot-8);}
        else if(name.StartsWith("Hired "))
            for(int i=0;i<opStaffVisuals.Length;i++)if(opStaffVisuals[i]==actor.person.root.gameObject){role=id="hired-"+i;break;}
        var root=new GameObject("Pixel Actor / "+name,typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(pixelRoom,false);root.anchorMin=root.anchorMax=new Vector2(0f,1f);root.pivot=new Vector2(.5f,.5f);root.sizeDelta=Vector2.zero;
        var shadow=new GameObject("Grounded Person Shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
        shadow.transform.SetParent(root,false);shadow.color=new Color32(22,24,35,96);shadow.raycastTarget=false;
        var image=PixelImage("Animated Pixel Person",root,pixelCharacters);
        var marker=PixelSolid("Live Order or Guest Marker",root);marker.gameObject.SetActive(false);
        var broom=PixelSolid("Actual Cleaner Broom",root);broom.color=new Color32(182,144,97,255);broom.gameObject.SetActive(false);
        var bristles=PixelSolid("Cleaner Broom Bristles",broom.transform);bristles.color=new Color32(199,178,126,255);
        string modelKey=actor.visual!=null?actor.visual.ModelKey:actor.person.root.name;
        uint variantSeed=PixelHash(id+"|"+modelKey);
        var view=new PixelActorView{actor=actor,root=root,image=image,shadow=shadow,marker=marker,broom=broom,bristles=bristles,id=id,role=role,originalRole=role,variantSeed=variantSeed,layer=new PixelLayer{root=root}};
        CreatePixelOrderBubble(view);
        BindPixelActorFurniture(view);
        return view;
    }

    private void CreatePixelOrderBubble(PixelActorView view)
    {
        var bubble=new GameObject("Order Bubble / "+view.id,typeof(RectTransform)).GetComponent<RectTransform>();bubble.SetParent(pixelOrderBubbles,false);PixelRect(bubble,Vector2.zero,new Vector2(32f,32f));view.orderBubble=bubble;
        Color border=new Color32(24,35,51,255),fill=new Color32(255,245,216,255);
        foreach(var rect in new[]{new Vector4(2,0,28,28),new Vector4(0,2,32,24)}){var image=PixelSolid("Bubble Border",bubble);image.color=border;PixelRect(image.rectTransform,new Vector2(rect.x,rect.y),new Vector2(rect.z,rect.w));}
        var tail=new GameObject("Bubble Tail Border",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();tail.transform.SetParent(bubble,false);tail.Initialize(null,new[]{new Vector2(12f,27f),new Vector2(20f,27f),new Vector2(16f,32f)});tail.color=border;
        view.orderTail=tail;
        foreach(var rect in new[]{new Vector4(2,1,28,26),new Vector4(1,2,30,24)}){var image=PixelSolid("Bubble Fill",bubble);image.color=fill;PixelRect(image.rectTransform,new Vector2(rect.x,rect.y),new Vector2(rect.z,rect.w));}
        var inset=new GameObject("Bubble Tail Fill",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();inset.transform.SetParent(bubble,false);inset.Initialize(null,new[]{new Vector2(14f,27f),new Vector2(18f,27f),new Vector2(16f,30f)});inset.color=fill;
        view.orderTailFill=inset;
        view.orderIcon=PixelImage("Requested Menu Item",bubble,null);PixelRect(view.orderIcon.rectTransform,new Vector2(4f,2f),new Vector2(24f,24f));bubble.gameObject.SetActive(false);
    }

    private void UpdatePixelOrderBubble(PixelActorView view,Vector2 point,float footY,float height,float crop)
    {
        int item=view.actor.person.drinkIndex;bool visible=JourneyIsPlaying&&item>=0&&item<DrinkNames.Length&&orderQueue.Contains(view.actor.person)
            &&pixelAtlases.TryGetValue("orders",out PixelAtlas atlas)&&item<atlas.columns&&pixelAtlasTextures.ContainsKey("orders");
        view.orderBubble.gameObject.SetActive(visible);if(!visible)return;
        Texture2D texture=pixelAtlasTextures["orders"];atlas=pixelAtlases["orders"];float width=atlas.frameWidth>0?atlas.frameWidth:32,heightPixels=atlas.frameHeight>0?atlas.frameHeight:32;
        float headOffset=Mathf.Round(footY*height/Mathf.Max(1f,crop))-4f;
        view.orderBase=new Vector2(Mathf.Round(point.x-16f),Mathf.Round(point.y-headOffset-3f-4f-28f));
        view.orderIcon.texture=texture;view.orderIcon.uvRect=new Rect(item*width/texture.width,1f-heightPixels/texture.height,width/texture.width,heightPixels/texture.height);
    }
    private void PlacePixelOrderBubbles()
    {
        pixelOrderCards.Clear();
        foreach(Patron patron in orderQueue)
        {
            if(!motionLookup.TryGetValue(patron,out MotionActor actor)||!pixelActorViews.TryGetValue(actor,out PixelActorView view)||!view.orderBubble.gameObject.activeSelf)continue;
            Vector2 point=view.orderBase;
            foreach(Vector2 offset in PixelOrderDodge)
            {
                Vector2 candidate=view.orderBase+offset;bool free=candidate.x>=2f&&candidate.x+32f<=958f&&candidate.y>=2f;
                foreach(Rect placed in pixelOrderCards)if(candidate.x<placed.x+34f&&candidate.x+34f>placed.x&&candidate.y<placed.y+30f&&candidate.y+30f>placed.y){free=false;break;}
                if(free){point=candidate;break;}
            }
            pixelOrderCards.Add(new Rect(point,new Vector2(32f,28f)));PixelRect(view.orderBubble,point,new Vector2(32f,32f));
            view.orderBubble.SetAsLastSibling();
            Vector2 tip=view.orderBase+new Vector2(16f,32f)-point;float neck=Mathf.Clamp(tip.x,5f,27f);
            view.orderTail.Initialize(null,new[]{new Vector2(neck-4f,27f),new Vector2(neck+4f,27f),tip});
            view.orderTailFill.Initialize(null,new[]{new Vector2(neck-2f,27f),new Vector2(neck+2f,27f),tip+new Vector2(0f,-2f)});
        }
    }
    private static uint PixelHash(string value)
    {
        uint result=2166136261;unchecked{foreach(char c in value)result=(result^c)*16777619;}return result;
    }
    private Vector2 PixelActorOffset(PixelActorView view,Vector3 world)
    {
        float stageWeight=view.actor.kind==MotionKind.Band&&view.actor.home.y>.01f?Mathf.Clamp01(world.y/view.actor.home.y):1f;
        foreach(PixelOffset value in pixelManifest.bandStageFeet??Array.Empty<PixelOffset>())
            if(value.id==view.id)return (value.offset-PixelProject(view.actor.home))*stageWeight;
        Vector2 offset=Vector2.zero;
        foreach(PixelOffset value in pixelManifest.actorOffsets??Array.Empty<PixelOffset>())if(value.id==view.id)offset+=value.offset*stageWeight;
        if(view.actor.kind==MotionKind.Band)offset.y-=pixelManifest.stageElevationPixels*stageWeight;
        PixelBar region=pixelManifest.barRegion;
        if(region!=null&&(view.role=="bartender"||view.role=="server"||view.role.StartsWith("hired-"))
            &&world.z>=region.zMin&&world.z<=region.zMax)
        {
            float weight=Mathf.Clamp01((region.xMax-world.x)/Mathf.Max(.01f,region.xFade));
            if(region.boundedX)weight*=Mathf.Clamp01((world.x-region.xMin)/Mathf.Max(.01f,region.xFade));
            if(region.zFade>0f)weight*=Mathf.Clamp01((world.z-region.zMin)/region.zFade)*Mathf.Clamp01((region.zMax-world.z)/region.zFade);
            offset+=region.offset*weight;
        }
        return offset;
    }
    private bool PixelBandJump(PixelActorView view,string action,bool active,out int frame,out float lift,out float shadow)
    {
        frame=-1;lift=0f;shadow=1f;
        if(!active||action!="perform"||(view.role!="guitarist"&&view.role!="bassist"))return false;
        float phase=Mathf.Repeat(pixelAnimationSeconds+(view.role=="bassist"?2.6f:0f),10.4f);
        if(phase<8.9f||phase>=9.9f)return false;
        float progress=phase-8.9f,air=Mathf.Max(0f,Mathf.Sin(Mathf.Clamp01((progress-.125f)/.75f)*Mathf.PI));
        frame=Mathf.Min(7,Mathf.FloorToInt(progress*8f));lift=Mathf.Round(air*7f);shadow=1f-air*.32f;return true;
    }
    private string PixelActionFor(MotionActor actor)
    {
        if(AfterShowOwns(actor))return actor.afterShowAction??"aftershow-wait";
        foreach(FloorWorkerActor worker in floorCleaningCrew)
            if(worker.actor==actor&&worker.route.ready&&FloorState!=null&&FloorState.cleanupActive&&!FloorState.cleanupComplete
                &&FloorState.reaction!=null&&FloorState.reaction.cleanupWorkBudget>0f)return "clean";
        if(actor.pitSlot==11&&actor.pitActive)return CrowdSurfWeight>.1f?"surf":"idle";
        if(actor.pitSlot>=8&&actor.pitSlot<=10&&actor.pitActive)return "support";
        if(actor.pitActive)return actor.pitSlot<6?"mosh":"slam";
        if(actor.moving)return "walk";
        if(actor.queueIndex>=0)return "queue";
        if(actor.kind==MotionKind.Band)return JourneyIsPlaying?"perform":"idle";
        if(actor.visual!=null&&actor.visual.ServingBlend>.12f)return "serve";
        if(actor.visual!=null&&actor.visual.IsWalking)return "walk";
        if(actor.kind==MotionKind.BarStaff)return "idle";
        if(actor.celebration>.05f||actor.person.isDancing)return "dance";
        return actor.person.isDancing?"dance":"idle";
    }
    private void UpdatePixelActor(PixelActorView view)
    {
        MotionActor actor=view.actor;Vector3 world=MotionPosition(actor);Vector2 offset=PixelActorOffset(view,world);
        PixelRole role=pixelRoles.TryGetValue(view.role,out PixelRole found)?found:pixelRoles["guest"];
        string action=PixelActionFor(actor);bool active=JourneyIsPlaying||JourneyIsFloorClosing;
        PixelBar bar=pixelManifest.barRegion;
        if(view.role=="bartender"&&!actor.moving&&(action=="idle"||action=="serve")&&bar!=null
            &&world.x<bar.xMax&&world.z>=bar.zMin&&world.z<=bar.zMax)action="idle";
        bool seated=view.seat!=null&&(view.role=="seated"||view.role=="barseated")&&actor.kind==MotionKind.Stationary&&!actor.moving&&(action=="idle"||action=="sit"||action=="drink");
        Vector2 p=seated?view.seat.feet:PixelProject(world)+offset;
        if(view.layer.after!=null)foreach(Transform mask in view.layer.after)mask.gameObject.SetActive(seated);
        bool hasJump=false;foreach(PixelAction candidate in role.actions??Array.Empty<PixelAction>())if(candidate.id=="jump"){hasJump=true;break;}
        int jumpFrame=-1;float jumpLift=0f,shadowScale=1f;
        if(hasJump&&PixelBandJump(view,action,active,out jumpFrame,out jumpLift,out shadowScale))action="jump";
        int framePhase=(int)(view.variantSeed%4);
        PixelAction selected=null;
        foreach(PixelAction candidate in role.actions??Array.Empty<PixelAction>())if(candidate.id==action){selected=candidate;break;}
        if(selected==null&&(action=="mosh"||action=="slam"||action=="surf"||action=="support"))
            foreach(PixelAction candidate in role.actions??Array.Empty<PixelAction>())if(candidate.id=="dance"){selected=candidate;break;}
        string wardrobe=actor.visual!=null?actor.visual.ModelKey:"Casual";
        if(role.styles!=null&&role.styles.Length>0)
        {
            bool supported=false;foreach(string style in role.styles)if(style==wardrobe){supported=true;break;}
            if(!supported)
            {
                string suffix=view.id.Substring(view.id.LastIndexOf('-')+1);
                int selection=int.TryParse(suffix,out int index)?index:actor.index;
                wardrobe=role.styles[Mathf.Abs(selection)%role.styles.Length];
            }
        }
        if(selected!=null&&selected.preserveStyle)
        {
            bool supported=false;foreach(PixelStyle style in selected.variantStyles??Array.Empty<PixelStyle>())if(style.id==wardrobe){supported=true;break;}
            if(!supported)selected=null;
        }
        int[] frames=selected?.frameColumns??role.frameColumns;
        int cycleLength=frames!=null&&frames.Length>0?frames.Length:4;
        if(frames==null||frames.Length==0)frames=PixelIdleFrame;
        float fps=selected!=null&&selected.fps>0f?selected.fps:role.fps>0f?role.fps:action=="walk"?7f:action=="clean"?6f:5f;
        int frame=selected?.progressFrames==true?Mathf.Min(frames.Length-1,Mathf.FloorToInt(Mathf.Clamp01(actor.afterShowProgress)*frames.Length))
            :active?Mathf.FloorToInt(pixelAnimationSeconds*fps+framePhase)%cycleLength:0;
        if(jumpFrame>=0)frame=jumpFrame;
        string atlasId=!string.IsNullOrEmpty(selected?.atlas)?selected.atlas:!string.IsNullOrEmpty(role.atlas)?role.atlas:!string.IsNullOrEmpty(pixelManifest.defaultAtlas)?pixelManifest.defaultAtlas:"animation";
        string[] atlasFrames=selected?.atlasFrames??role.atlasFrames;
        if(atlasFrames!=null&&atlasFrames.Length>0)atlasId=atlasFrames[frame%atlasFrames.Length];
        PixelAtlas atlas=pixelAtlases.TryGetValue(atlasId,out PixelAtlas sheet)?sheet:null;
        Texture2D texture=pixelAtlasTextures.TryGetValue(atlasId,out Texture2D source)?source:pixelCharacters;
        int frameWidth=atlas!=null&&atlas.frameWidth>0?atlas.frameWidth:pixelManifest.frameWidth;
        int frameHeight=atlas!=null&&atlas.frameHeight>0?atlas.frameHeight:pixelManifest.frameHeight;
        int[] rows=selected?.variantRows??role.variantRows;
        int col=frames[frame%frames.Length],row=rows!=null&&rows.Length>0?rows[view.variantSeed%(uint)rows.Length]:selected!=null&&selected.hasRow?selected.row:role.row;
        PixelStyle[] styles=selected?.variantStyles!=null&&selected.variantStyles.Length>0?selected.variantStyles
            :selected==null||!selected.hasRow?role.variantStyles:null;
        foreach(PixelStyle style in styles??Array.Empty<PixelStyle>())if(style.id==wardrobe){row=style.row;break;}
        Vector3 forward=PixelFacingForward(view,world);
        string direction=(Vector3.Dot(forward,PixelForward)<0f?"F":"B")+(Vector3.Dot(forward,PixelRight)>=0f?"R":"L");
        if(!string.IsNullOrEmpty(selected?.direction))direction=selected.direction;
        else if(!string.IsNullOrEmpty(role.direction))direction=role.direction;
        PixelStyle[] directionRows=selected?.directionRows!=null&&selected.directionRows.Length>0?selected.directionRows:role.directionRows;
        PixelStyle[] rowOffsets=selected?.directionRowOffsets!=null&&selected.directionRowOffsets.Length>0?selected.directionRowOffsets:role.directionRowOffsets;
        foreach(PixelStyle facing in directionRows??Array.Empty<PixelStyle>())if(facing.id==direction){row=facing.row;break;}
        foreach(PixelStyle facing in rowOffsets??Array.Empty<PixelStyle>())if(facing.id==direction){row+=facing.row;break;}
        PixelDirection[] directions=selected?.directionColumns!=null&&selected.directionColumns.Length>0?selected.directionColumns
            :role.directionColumns!=null&&role.directionColumns.Length>0?role.directionColumns:atlas?.directionColumns;
        bool directional=directions!=null&&directions.Length>0||directionRows!=null&&directionRows.Length>0||rowOffsets!=null&&rowOffsets.Length>0;
        foreach(PixelDirection facing in directions??Array.Empty<PixelDirection>())if(facing.id==direction){col=facing.column;break;}
        float crop=selected!=null&&selected.cropHeight>0?selected.cropHeight:role.cropHeight>0?role.cropHeight:atlas!=null&&atlas.cropHeight>0?atlas.cropHeight:frameHeight;
        float height=selected!=null&&selected.drawHeight>0?selected.drawHeight:role.drawHeight>0?role.drawHeight:atlas!=null&&atlas.drawHeight>0?atlas.drawHeight:pixelManifest.drawHeight,scale=height/crop;
        float footX=selected!=null&&selected.footX>0?selected.footX:role.footX>0?role.footX:atlas!=null&&atlas.footX>0?atlas.footX:pixelManifest.footX;
        float footY=selected!=null&&selected.footY>0?selected.footY:role.footY>0?role.footY:atlas!=null&&atlas.footY>0?atlas.footY:pixelManifest.footY;
        view.image.texture=texture;
        view.image.uvRect=new Rect(col*frameWidth/(float)texture.width,
            1f-(row*frameHeight+crop)/texture.height,
            frameWidth/(float)texture.width,crop/texture.height);
        RectTransform image=view.image.rectTransform;image.anchorMin=image.anchorMax=new Vector2(.5f,.5f);
        image.pivot=new Vector2(footX/frameWidth,1f-footY/crop);
        image.sizeDelta=new Vector2(frameWidth*scale,height);
        ApplyPerformingBandImage(view,action,active);
        float yaw=actor.person.root.localEulerAngles.y;
        float yawSin=Mathf.Sin(yaw*Mathf.Deg2Rad);
        bool flip=!directional&&yawSin<-.18f;
        float bob=active&&action=="dance"?Mathf.Round(Mathf.Sin(pixelAnimationSeconds*5f+(view.variantSeed%9))*0.6f):0f;
        view.root.anchoredPosition=new Vector2(Mathf.Round(p.x),-Mathf.Round(p.y+bob));
        view.root.localScale=new Vector3(flip?-1f:1f,1f,1f);
        bool surf=actor.pitSlot==11&&CrowdSurfWeight>.1f&&actor.pitActive;
        bool horizontal=selected?.horizontal==true||role.horizontal;
        image.anchoredPosition=(surf&&!horizontal?new Vector2(-7f,0f):Vector2.zero)+new Vector2(0f,jumpLift);
        image.localRotation=Quaternion.Euler(0f,0f,surf&&!horizontal?90f:0f);
        view.shadow.gameObject.SetActive(!(seated&&view.seat.hideShadow)&&!(view.role=="drummer"&&action=="perform"));
        var shadowPoints=new Vector2[12];for(int i=0;i<shadowPoints.Length;i++){float angle=i*Mathf.PI/6f;shadowPoints[i]=new Vector2(Mathf.Cos(angle)*4f*shadowScale,1f+Mathf.Sin(angle));}
        view.shadow.Initialize(null,shadowPoints);view.shadow.color=new Color32(22,24,35,(byte)(jumpFrame>=0?72:96));
        bool assisted=actor==floorMissionGuestActor&&FloorState!=null&&FloorState.guestActive;
        view.marker.gameObject.SetActive(assisted);
        if(assisted){view.marker.color=new Color32(244,200,110,255);PixelRect(view.marker.rectTransform,new Vector2(-2f,-49f),new Vector2(4f,7f));}
        UpdatePixelOrderBubble(view,p,footY,height,crop);
        bool cleaning=action=="clean";view.broom.gameObject.SetActive(cleaning);
        if(cleaning)
        {
            var shaft=view.broom.rectTransform;shaft.anchorMin=shaft.anchorMax=new Vector2(.5f,.5f);shaft.pivot=new Vector2(.5f,1f);
            shaft.anchoredPosition=new Vector2(5f,17f);shaft.sizeDelta=new Vector2(1.5f,20f);
            shaft.localRotation=Quaternion.Euler(0f,0f,-18f+Mathf.Sin(pixelAnimationSeconds*7f+actor.index)*12f);
            var bristles=view.bristles.rectTransform;bristles.anchorMin=bristles.anchorMax=new Vector2(.5f,0f);
            bristles.pivot=new Vector2(.5f,1f);bristles.anchoredPosition=Vector2.zero;bristles.sizeDelta=new Vector2(7f,3f);
        }
        float depth=seated?view.seat.depth:PixelProject(new Vector3(world.x,0f,world.z)).y+offset.y;
        if(view.role=="drummer"&&action=="perform"&&pixelManifest.drumForegroundDepth>0f)depth=Mathf.Min(depth,pixelManifest.drumForegroundDepth-1f);
        view.layer.depth=depth;pixelDrawLayers.Add(view.layer);
    }
    private Vector3 PixelFacingForward(PixelActorView view,Vector3 world)
    {
        MotionActor actor=view.actor;
        if(AfterShowOwns(actor)){Vector3 heading=worldRoot.InverseTransformDirection(actor.person.root.forward);heading.y=0f;return heading.normalized;}
        // Face the surface or person being used while leaving the actual motion
        // transforms under the existing simulation's exclusive control.
        if(view.role=="barseated")return Vector3.forward;
        if(view.role=="seated")return world.z>0f?Vector3.back:Vector3.forward;
        if(actor.kind==MotionKind.Band)return new Vector3(ConcertPitCenter.x-world.x,0f,ConcertPitCenter.z-world.z).normalized;
        if(actor.pitActive&&actor.pitSlot>=0&&actor.pitSlot<6)
        {Vector3 radial=world-ConcertPitCenter;return new Vector3(-radial.z,0f,radial.x).normalized;}
        if(actor.pitActive&&(actor.pitSlot==6||actor.pitSlot==7))return actor.pitSlot==6?Vector3.right:Vector3.left;
        if(actor.pitActive&&actor.pitSlot>=8&&actor.pitSlot<=10&&concertActors[11]?.person.root!=null)
        {Vector3 toward=MotionPosition(concertActors[11])-world;toward.y=0f;return toward.normalized;}
        if(actor.queueIndex>=0&&!actor.moving)
        {
            Vector3 employee=new Vector3(-4.2f,0f,-.565f);
            foreach(MotionActor other in motionActors)if(other.person.name==(IsFreebirdLayout?"Bartender":"Server")){employee=MotionPosition(other);break;}
            Vector3 toward=employee-world;toward.y=0f;return toward.normalized;
        }
        if((view.role=="bartender"||view.role=="server")&&!actor.moving&&!IsFloorMissionActor(actor.person.root))return IsFFLayout?Vector3.right:Vector3.back;
        if(view.role=="guest"&&actor.kind==MotionKind.Guest&&!actor.moving)
            return new Vector3(5.5f-world.x,0f,2.7f-world.z).normalized;
        Vector3 forward=worldRoot.InverseTransformDirection(actor.person.root.forward);forward.y=0f;return forward.normalized;
    }
    private void UpdatePixelPaper()
    {
        var state=FloorState;
        float remaining=state==null||state.litter<=0?0f:Mathf.Clamp01(1f-state.cleaned/state.litter);
        int confetti=state==null?0:Mathf.CeilToInt(Mathf.Min(64,state.confettiThrows*24)*remaining);
        int tissueCount=state==null?0:Mathf.CeilToInt(Mathf.Min(40,state.tissueThrows*16)*remaining);
        for(int i=0;i<pixelPaper.Count;i++)
        {
            bool tissue=i>=64;int index=tissue?i-64:i;bool visible=index<(tissue?tissueCount:confetti);
            var image=pixelPaper[i];image.gameObject.SetActive(visible);if(!visible)continue;
            image.color=tissue?new Color32(232,226,213,255):PixelPaperColors[index%4];image.SetPaper(tissue,false,index);
            PixelRect(image.rectTransform,new Vector2(Mathf.Floor(pixelPaperPositions[i].x+.5f),Mathf.Floor(pixelPaperPositions[i].y+.5f)),new Vector2(16f,20f));
        }
    }
    private void BurstPixelPaper(bool tissue,int serial)
    {
        var random=new PixelPaperRandom((uint)(3917+serial*97+(tissue?7001:0)));
        for(int i=0;i<(tissue?28:56);i++)
        {
            PixelParticle particle=null;
            foreach(var candidate in pixelParticles)
                if(!candidate.image.gameObject.activeSelf){particle=candidate;break;}
                else if(particle==null||candidate.sequence<particle.sequence)particle=candidate;
            if(particle==null)return;
            particle.tissue=tissue;particle.age=0f;particle.index=i;particle.sequence=++pixelPaperSequence;
            float x=3f+random.Next()*5f,z=2.75f+random.Next()*.65f,y=2.6f+random.Next()*1.6f;
            float vx=(random.Next()-.5f)*1.4f,vz=-.65f-random.Next()*1.15f,vy=(tissue?.5f:1f)+random.Next()*(tissue?.8f:1.2f);
            particle.position=new Vector3(x,y,z);particle.velocity=new Vector3(vx,vy,vz);particle.phase=random.Next()*Mathf.PI*2f;
            particle.image.color=tissue?new Color32(255,247,232,255):PixelPaperColors[i%4];particle.image.SetPaper(tissue,true,i,0f,particle.phase);
            particle.image.gameObject.SetActive(true);
        }
    }
    private void UpdateAirbornePixelPaper(float delta)
    {
        if(FloorState==null)return;
        ObservePixelPaperState();
        // A saved old throw stays on the floor. Only new performances burst.
        while(pixelConfettiSeen<FloorState.confettiThrows)BurstPixelPaper(false,++pixelConfettiSeen);
        while(pixelTissueSeen<FloorState.tissueThrows)BurstPixelPaper(true,++pixelTissueSeen);
        foreach(var particle in pixelParticles)
        {
            if(!particle.image.gameObject.activeSelf)continue;particle.age+=delta;
            particle.velocity.y-=delta*(particle.tissue?.8f:1.3f);particle.position+=particle.velocity*delta;
            if(particle.position.y<.03f||particle.age>8f||particle.position.x>10.3f||particle.position.z< -7f){particle.image.gameObject.SetActive(false);continue;}
            Vector2 p=PixelProject(particle.position);p.x+=Mathf.Sin(particle.phase+particle.age*6f)*1.5f;
            PixelRect(particle.image.rectTransform,new Vector2(Mathf.Floor(p.x+.5f),Mathf.Floor(p.y+.5f)),new Vector2(16f,20f));
            particle.image.SetPaper(particle.tissue,true,particle.index,particle.age,particle.phase);
        }
    }
    private void NotifyPixelPaperBurst(int effect,int serial)
    {
        if(pixelAirborne==null)return;
        ObservePixelPaperState();
        bool tissue=effect==SimBarFloorMissions.Tissue;
        BurstPixelPaper(tissue,serial);
        if(tissue)pixelTissueSeen=serial;else pixelConfettiSeen=serial;
    }
    private void ObservePixelPaperState()
    {
        int session=journey==null?0:journey.session;
        if(pixelSession!=session||!ReferenceEquals(pixelObservedFloorState,FloorState)
            ||pixelConfettiSeen>FloorState.confettiThrows||pixelTissueSeen>FloorState.tissueThrows)
        {
            pixelSession=session;pixelObservedFloorState=FloorState;
            pixelConfettiSeen=FloorState.confettiThrows;pixelTissueSeen=FloorState.tissueThrows;
            pixelPaperSequence=0;
            foreach(var particle in pixelParticles)particle.image.gameObject.SetActive(false);
        }
    }
    private void LateUpdate()
    {
        if(pixelWorldCanvas==null)return;
        bool visible=worldRoot!=null&&worldRoot.gameObject.activeInHierarchy;pixelWorldCanvas.enabled=visible;if(!visible)return;
        RefreshPixelVenue();
        RefreshPerformingBand();
        if(pixelScreenWidth!=Screen.width||pixelScreenHeight!=Screen.height)FitPixelPresentation();
        float delta=!paused&&(JourneyIsPlaying||JourneyIsFloorClosing)?Mathf.Min(.1f,Time.unscaledDeltaTime):0f;
        UpdateAlbumJacket();
        UpdatePixelLights();
        pixelAnimationSeconds+=delta;
        // The 3D night-backdrop component can add its own layer on first activation.
        // Keep the camera clear while this separately composited pixel world is active.
        Camera main=Camera.main;if(main!=null&&main.cullingMask!=0)main.cullingMask=0;
        pixelDrawLayers.Clear();pixelDrawLayers.AddRange(pixelStaticLayers);
        pixelRemovedActors.Clear();
        foreach(var pair in pixelActorViews)if(pair.Key.person.root==null||!motionActors.Contains(pair.Key))pixelRemovedActors.Add(pair.Key);
        foreach(var actor in pixelRemovedActors)
        {
            var view=pixelActorViews[actor];Destroy(view.root.gameObject);
            if(view.orderBubble!=null)Destroy(view.orderBubble.gameObject);
            if(view.layer.after!=null)foreach(Transform mask in view.layer.after)
            {
                SimBarPixelPolygonGraphic graphic=mask.GetComponent<SimBarPixelPolygonGraphic>();
                pixelVenueMasks.RemoveAll(value=>value.graphic==graphic);
                Destroy(mask.gameObject);
            }
            pixelActorViews.Remove(actor);
        }
        foreach(MotionActor actor in motionActors)
        {
            if(actor.person.root==null)continue;
            if(!pixelActorViews.TryGetValue(actor,out PixelActorView view)){view=CreatePixelActor(actor);pixelActorViews.Add(actor,view);}
            bool active=actor.person.root.gameObject.activeInHierarchy;view.root.gameObject.SetActive(active);
            if(active)UpdatePixelActor(view);else{if(view.orderBubble!=null)view.orderBubble.gameObject.SetActive(false);if(view.layer.after!=null)foreach(Transform mask in view.layer.after)mask.gameObject.SetActive(false);}
        }
        UpdateExtraPerformingBand();
        UpdatePixelMiniMap();
        UpdatePixelPaper();pixelDrawLayers.Sort((a,b)=>a.depth.CompareTo(b.depth));
        foreach(PixelLayer layer in pixelDrawLayers)
        {
            layer.root.SetAsLastSibling();
            if(layer.after!=null)foreach(Transform mask in layer.after)mask.SetAsLastSibling();
        }
        UpdateAirbornePixelPaper(delta);pixelAirborne.SetAsLastSibling();PlacePixelOrderBubbles();pixelOrderBubbles.SetAsLastSibling();
    }

    private void UpdatePixelMiniMap()
    {
        if(pixelMiniMap==null&&worldRoot!=null)pixelMiniMap=worldRoot.GetComponentInChildren<SimBarMiniMap>(true);
        if(pixelMiniMap==null)return;
        bool pixelOverview=selectedOwner!=2||activePerformingBand!=null;
        pixelMiniMap.BeginPixelOverview(pixelOverview?pixelVenueTexture:null);
        if(!pixelOverview)return;
        int propIndex=0;
        foreach(PixelPropView prop in pixelPropViews)
        {
            PixelProp data=prop.definition;
            pixelMiniMap.SetPixelProp(propIndex++,prop.texture,PixelProject(new Vector3(data.x,0f,data.z)),new Vector2(data.drawWidth,data.drawHeight),new Vector2(data.footX,data.footY));
        }
        pixelMiniMap.EndPixelProps(propIndex);
        int index=0;
        foreach(var pair in pixelActorViews)
        {
            PixelActorView view=pair.Value;if(!view.root.gameObject.activeSelf)continue;
            MotionActor actor=view.actor;bool employee=actor.kind==MotionKind.BarStaff||actor.kind==MotionKind.FloorStaff;
            Color color=actor==floorMissionGuestActor?new Color32(255,211,111,255):employee?new Color32(110,229,154,255):actor.kind==MotionKind.Band?new Color32(101,219,239,255):new Color32(245,228,200,255);
            Vector2 point=view.root.anchoredPosition;point.y=-point.y;
            pixelMiniMap.SetPixelMarker(index++,point,color);
        }
        foreach(ExtraPerformingBandView view in extraPerformingBandViews)
            if(view.root!=null&&view.root.gameObject.activeSelf)pixelMiniMap.SetPixelMarker(index++,view.point,new Color32(101,219,239,255));
        pixelMiniMap.EndPixelOverview(index);
    }
}
