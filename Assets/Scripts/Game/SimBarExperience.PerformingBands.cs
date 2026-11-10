using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Artist-specific presentation follows the existing, saved band actors.</summary>
public sealed partial class SimBarExperience
{
    [Serializable] private sealed class PerformingBandTrack { public string id,band; }
    [Serializable] private sealed class PerformingBandAction
    {
        public string id;public int[] frames;public float fps;public bool progress;
    }
    [Serializable] private sealed class PerformingBandMember
    {
        public string id,name,role,anchor,from,to;public int row;public bool extra;
        public float mix,drawWidth,drawHeight;public Vector2 stageOffset,floorOffset;
    }
    [Serializable] private sealed class PerformingBandDefinition
    {
        public string id,resource;public PerformingBandMember[] members;public PerformingBandAction[] actions;
    }
    [Serializable] private sealed class PerformingBandManifest
    {
        public int frameWidth=64,frameHeight=64,columns=12,footX=32,footY=60;
        public float drawWidth=54f,drawHeight=54f;
        public PerformingBandTrack[] tracks;public PerformingBandDefinition[] bands;public PerformingBandAction[] actions;
    }
    private sealed class ExtraPerformingBandView
    {
        public RectTransform root;public RawImage image;public SimBarPixelPolygonGraphic shadow;
        public PixelLayer layer;public Vector2 point;public PerformingBandMember member;
    }
    private PerformingBandManifest performingBandManifest;
    private PerformingBandDefinition activePerformingBand;
    private Texture2D activePerformingTexture;
    private bool performingBandManifestLoaded;
    private readonly List<ExtraPerformingBandView> extraPerformingBandViews=new List<ExtraPerformingBandView>(2);
    private static readonly string[] PerformingCanonicalTrackIds={"track_001","track_002","track_003","track_004","track_005"};
    private static readonly int[] PerformingFrames={0,1,2,3},PerformingWalkFrames={4,5,6,7},PerformingIdleFrames={8},PerformingHighFiveFrames={8,9,9,8},PerformingHugFrames={8,10,11,8};

    public string CurrentPerformingBandId=>activePerformingBand?.id??"default";
    public int VisiblePerformingModelCount
    {
        get
        {
            if(pixelWorldCanvas==null||!pixelWorldCanvas.enabled)return 0;
            int count=0;
            foreach(PixelActorView view in pixelActorViews.Values)
                if(view.actor.kind==MotionKind.Band&&view.root.gameObject.activeInHierarchy)count++;
            foreach(ExtraPerformingBandView view in extraPerformingBandViews)
                if(view.root!=null&&view.root.gameObject.activeInHierarchy)count++;
            return count;
        }
    }

    private void RefreshPerformingBand()
    {
        if(!performingBandManifestLoaded)
        {
            performingBandManifestLoaded=true;
            TextAsset raw=Resources.Load<TextAsset>("SimBarPixelArt/PerformingBands");
            if(raw!=null)
            {
                try{performingBandManifest=JsonUtility.FromJson<PerformingBandManifest>(raw.text);}
                catch(Exception error){Debug.LogWarning("Could not read performing-band models: "+error.Message);}
                Resources.UnloadAsset(raw);
            }
        }
        string trackId=playlistIndex>=0&&playlistIndex<PerformingCanonicalTrackIds.Length?PerformingCanonicalTrackIds[playlistIndex]:null;
        string artist=null;
        if(streamingMusicTracks!=null&&playlistIndex>=0&&playlistIndex<streamingMusicTracks.Length)
        {trackId=streamingMusicTracks[playlistIndex].id;artist=streamingMusicTracks[playlistIndex].artist;}
        string bandId=null;
        foreach(PerformingBandTrack track in performingBandManifest?.tracks??Array.Empty<PerformingBandTrack>())
            if(track.id==trackId){bandId=track.band;break;}
        if(bandId==null&&!string.IsNullOrEmpty(artist))
        {
            string normalized=artist.ToLowerInvariant();
            if(normalized.Contains("freewill"))bandId="freewill";
            else if(normalized.Contains("julywantsmore"))bandId="julywantsmore";
        }
        PerformingBandDefinition next=null;
        foreach(PerformingBandDefinition band in performingBandManifest?.bands??Array.Empty<PerformingBandDefinition>())
            if(band.id==bandId){next=band;break;}
        Texture2D texture=next!=null&&!string.IsNullOrEmpty(next.resource)?LoadPixelTexture(next.resource):null;
        activePerformingBand=texture!=null?next:null;activePerformingTexture=texture;
    }

    private PixelActorView PerformingAnchor(string id)
    {
        foreach(PixelActorView view in pixelActorViews.Values)if(view.id==id)return view;
        return null;
    }
    private static Vector2 PerformingPoint(PixelActorView view)=>new Vector2(view.root.anchoredPosition.x,-view.root.anchoredPosition.y);
    private Vector2 PerformingStagePoint(PixelActorView view)
    {
        foreach(PixelOffset point in pixelManifest.bandStageFeet??Array.Empty<PixelOffset>())if(point.id==view.id)return point.offset;
        return PixelProject(view.actor.home)+PixelActorOffset(view,view.actor.home);
    }
    private float PerformingStageLevel(PixelActorView anchor)
    {
        if(StagePerformanceLightingActive)return 1f;
        if(journey?.afterShow?.members!=null)
            foreach(AfterShowMember member in journey.afterShow.members)if(member.id==anchor.id)return Mathf.Clamp01(member.stageLevel);
        Vector3 world=MotionPosition(anchor.actor);
        return anchor.actor.home.y>.01f?Mathf.Clamp01(world.y/anchor.actor.home.y):1f;
    }
    private int PerformingColumn(PerformingBandMember member,string action,MotionActor actor,bool active)
    {
        PerformingBandAction selected=null;
        foreach(PerformingBandAction candidate in activePerformingBand?.actions??performingBandManifest?.actions??Array.Empty<PerformingBandAction>())
            if(candidate.id==action){selected=candidate;break;}
        int[] frames=selected?.frames;
        if(frames==null||frames.Length==0)
        {
            if(action=="perform")frames=PerformingFrames;
            else if(action=="walk")frames=PerformingWalkFrames;
            else if(action=="highfive")frames=PerformingHighFiveFrames;
            else if(action=="hug")frames=PerformingHugFrames;
            else frames=PerformingIdleFrames;
        }
        bool progress=selected?.progress==true||action=="highfive"||action=="hug";
        int index=progress?Mathf.Min(frames.Length-1,Mathf.FloorToInt(Mathf.Clamp01(actor.afterShowProgress)*frames.Length))
            :active&&frames.Length>1?Mathf.FloorToInt(pixelAnimationSeconds*(selected!=null&&selected.fps>0f?selected.fps:6f)+(PixelHash(member.id??member.role)%4))%frames.Length:0;
        return Mathf.Clamp(frames[index],0,Mathf.Max(1,performingBandManifest.columns)-1);
    }
    private void SetPerformingImage(RawImage image,PerformingBandMember member,string action,MotionActor actor,bool active)
    {
        int fw=Mathf.Max(1,performingBandManifest.frameWidth),fh=Mathf.Max(1,performingBandManifest.frameHeight);
        int column=PerformingColumn(member,action,actor,active),row=Mathf.Max(0,member.row);
        image.texture=activePerformingTexture;
        image.uvRect=new Rect(column*fw/(float)activePerformingTexture.width,
            1f-(row+1)*fh/(float)activePerformingTexture.height,fw/(float)activePerformingTexture.width,fh/(float)activePerformingTexture.height);
        float height=member.drawHeight>0f?member.drawHeight:performingBandManifest.drawHeight;
        float width=member.drawWidth>0f?member.drawWidth:performingBandManifest.drawWidth;
        image.rectTransform.pivot=new Vector2(performingBandManifest.footX/(float)fw,1f-performingBandManifest.footY/(float)fh);
        image.rectTransform.sizeDelta=new Vector2(width,height);
    }
    private void ApplyPerformingBandImage(PixelActorView view,string action,bool active)
    {
        if(view.actor.kind!=MotionKind.Band||activePerformingBand==null)return;
        if(action=="jump")action="perform";
        foreach(PerformingBandMember member in activePerformingBand.members??Array.Empty<PerformingBandMember>())
            if(!member.extra&&member.anchor==view.id){SetPerformingImage(view.image,member,action,view.actor,active);return;}
    }
    private ExtraPerformingBandView ExtraPerformingView(int index)
    {
        while(extraPerformingBandViews.Count<=index)
        {
            var root=new GameObject("Track Artist Additional Musician "+extraPerformingBandViews.Count,typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(pixelRoom,false);root.anchorMin=root.anchorMax=new Vector2(0f,1f);root.pivot=new Vector2(.5f,.5f);root.sizeDelta=Vector2.zero;
            var image=PixelImage("Artist Model",root,null);image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(.5f,.5f);image.rectTransform.anchoredPosition=Vector2.zero;
            var shadow=new GameObject("Artist Ground Contact",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
            shadow.transform.SetParent(root,false);shadow.color=new Color32(22,24,35,96);
            var points=new Vector2[12];for(int i=0;i<points.Length;i++){float angle=i*Mathf.PI/6f;points[i]=new Vector2(Mathf.Cos(angle)*4f,1f+Mathf.Sin(angle));}
            shadow.Initialize(null,points);shadow.transform.SetAsFirstSibling();
            extraPerformingBandViews.Add(new ExtraPerformingBandView{root=root,image=image,shadow=shadow,layer=new PixelLayer{root=root}});
        }
        return extraPerformingBandViews[index];
    }
    private void UpdateExtraPerformingBand()
    {
        int count=0;
        foreach(PerformingBandMember member in activePerformingBand?.members??Array.Empty<PerformingBandMember>())
        {
            if(!member.extra)continue;
            PixelActorView anchor=PerformingAnchor(string.IsNullOrEmpty(member.anchor)?"guitarist":member.anchor);
            if(anchor==null||!anchor.root.gameObject.activeInHierarchy)continue;
            PixelActorView from=PerformingAnchor(member.from),to=PerformingAnchor(member.to);
            Vector2 floor=PerformingPoint(anchor)+member.floorOffset;
            Vector2 stage=from!=null&&to!=null?Vector2.Lerp(PerformingStagePoint(from),PerformingStagePoint(to),member.mix)+member.stageOffset:PerformingStagePoint(anchor)+member.stageOffset;
            Vector2 point=Vector2.Lerp(floor,stage,PerformingStageLevel(anchor));
            ExtraPerformingBandView view=ExtraPerformingView(count++);view.member=member;view.point=point;
            view.root.gameObject.SetActive(true);view.root.anchoredPosition=new Vector2(Mathf.Round(point.x),-Mathf.Round(point.y));
            string action=PixelActionFor(anchor.actor);if(action!="walk"&&action!="perform")action="idle";
            SetPerformingImage(view.image,member,action,anchor.actor,JourneyIsPlaying||JourneyIsFloorClosing);
            view.layer.depth=point.y;pixelDrawLayers.Add(view.layer);
        }
        for(int i=count;i<extraPerformingBandViews.Count;i++)extraPerformingBandViews[i].root.gameObject.SetActive(false);
    }
    private void DestroyPerformingBandPresentation()
    {
        foreach(ExtraPerformingBandView view in extraPerformingBandViews)if(view.root!=null)Destroy(view.root.gameObject);
        extraPerformingBandViews.Clear();activePerformingBand=null;activePerformingTexture=null;
        performingBandManifest=null;performingBandManifestLoaded=false;
    }
}
