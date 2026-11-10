using System.Collections.Generic;
using UnityEngine;

public sealed partial class SimBarExperience
{
    private Transform pixelStageOffRoot;
    private readonly List<Transform> pixelStageOffOccluders=new List<Transform>();
    private static readonly Vector2[] PixelStageDeck={new Vector2(556f,299f),new Vector2(612f,242f),new Vector2(639f,209f),new Vector2(854f,266f),new Vector2(867f,311f),new Vector2(789f,373f)};
    private static readonly Color PixelStageDim=new Color(5f/255f,9f/255f,18f/255f,.72f);
    private Vector2[] ActivePixelStageDeck=>pixelManifest?.stageDeck?.Length>=3?pixelManifest.stageDeck:PixelStageDeck;
    private bool StagePerformanceLightingEnded=>journeyPhase==JourneyPhase.FloorClosing||JourneyIsResults||journey.floorClosing||journey.afterShow!=null&&journey.afterShow.started||journey.session>0&&journey.secondsLeft<=0f;

    private SimBarPixelPolygonGraphic PixelStageOffPolygon(string name,Transform parent,Vector2[] points,Color color)
    {
        var graphic=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
        graphic.transform.SetParent(parent,false);graphic.Initialize(null,points);graphic.color=color;return graphic;
    }
    private static Vector2[] PixelStageEllipse(Vector2 center,int rx,int ry)
    {
        // Match the native one-pixel scanlines, including their stepped silhouette.
        var points=new List<Vector2>();
        for(int row=-ry;row<=ry;row++){int w=Mathf.RoundToInt(rx*Mathf.Sqrt(Mathf.Max(0f,1f-row*row/(float)(ry*ry))));points.Add(center+new Vector2(w+1,row));points.Add(center+new Vector2(w+1,row+1));}
        for(int row=ry;row>=-ry;row--){int w=Mathf.RoundToInt(rx*Mathf.Sqrt(Mathf.Max(0f,1f-row*row/(float)(ry*ry))));points.Add(center+new Vector2(-w,row+1));points.Add(center+new Vector2(-w,row));}
        for(int i=points.Count-1;i>=0;i--){int previous=(i+points.Count-1)%points.Count,next=(i+1)%points.Count;if(points[i]==points[previous]||Mathf.Abs(PixelLightCross(points[i]-points[previous],points[next]-points[i]))<.001f)points.RemoveAt(i);}
        return points.ToArray();
    }
    private void InitializePixelStageOff()
    {
        pixelStageOffRoot=new GameObject("Ended Stage Unlit Surface",typeof(RectTransform)).transform;pixelStageOffRoot.SetParent(pixelRoom,false);PixelRect((RectTransform)pixelStageOffRoot,Vector2.zero,new Vector2(960f,558f));
        PixelStageOffPolygon("Stage Deck Without Concert Pools",pixelStageOffRoot,ActivePixelStageDeck,PixelStageDim);
        Vector2[] lamps=pixelManifest?.stageLamps?.Length>0?pixelManifest.stageLamps:new[]{new Vector2(643f,121f),new Vector2(683f,128f),new Vector2(735f,140f),new Vector2(782f,151f),new Vector2(822f,162f)};
        foreach(Vector2 center in lamps)
        {
            PixelStageOffPolygon("Unlit Fixture Outer Glass",pixelStageOffRoot,PixelStageEllipse(center,12,11),new Color(7f/255f,11f/255f,19f/255f,.45f));
            PixelStageOffPolygon("Unlit Fixture Glass",pixelStageOffRoot,PixelStageEllipse(center,10,9),new Color(7f/255f,11f/255f,19f/255f,.70f));
            PixelStageOffPolygon("Unlit Fixture Lens",pixelStageOffRoot,PixelStageEllipse(center,7,6),new Color32(23,32,44,255));
            PixelStageOffPolygon("Unlit Fixture Center",pixelStageOffRoot,PixelStageEllipse(center,4,3),new Color32(11,17,26,255));
            var glint=PixelSolid("Unlit Glass Reflection",pixelStageOffRoot);glint.color=new Color32(60,70,82,255);PixelRect(glint.rectTransform,center+new Vector2(-2f,-2f),new Vector2(2f,1f));
        }
        pixelStageOffRoot.gameObject.SetActive(false);
    }
    private Transform CreatePixelStageOffOccluder(Vector2[] points)
    {
        // Foreground drum/cymbal pixels are restored after actors. Apply the same
        // stage-only dimming to their intersection so they cannot restore a pool.
        // The deck has one small concave notch: these non-overlapping convex
        // pieces preserve it instead of incorrectly clipping against its hull.
        Vector2[] deck=ActivePixelStageDeck;
        Vector2[][] deckParts=deck.Length==6?new[]{new[]{deck[5],deck[0],deck[1]},new[]{deck[1],deck[2],deck[3],deck[4],deck[5]}}:new[]{deck};
        Transform group=null;
        foreach(Vector2[] part in deckParts)
        {
            Vector2[] clipped=ClipPixelStagePolygon(points,part);if(clipped.Length<3)continue;
            if(group==null){group=new GameObject("Unlit Stage Furniture Foreground",typeof(RectTransform)).transform;group.SetParent(pixelRoom,false);PixelRect((RectTransform)group,Vector2.zero,new Vector2(960f,558f));}
            PixelStageOffPolygon("Stage Deck Intersection",group,clipped,PixelStageDim);
        }
        if(group!=null){group.gameObject.SetActive(false);pixelStageOffOccluders.Add(group);}return group;
    }
    private static Vector2[] ClipPixelStagePolygon(Vector2[] points,Vector2[] boundary)
    {
        var clipped=new List<Vector2>(points);
        for(int edge=0;edge<boundary.Length&&clipped.Count>0;edge++)
        {
            Vector2 a=boundary[edge],b=boundary[(edge+1)%boundary.Length];var input=clipped;clipped=new List<Vector2>();
            Vector2 previous=input[input.Count-1];float previousSide=PixelLightCross(b-a,previous-a);
            foreach(Vector2 current in input)
            {
                float side=PixelLightCross(b-a,current-a);
                if((side>=0f)!=(previousSide>=0f))clipped.Add(previous+(current-previous)*(previousSide/(previousSide-side)));
                if(side>=0f)clipped.Add(current);previous=current;previousSide=side;
            }
        }
        return clipped.ToArray();
    }
    private void UpdatePixelStageOff()
    {
        bool ended=StagePerformanceLightingEnded;
        if(pixelStageOffRoot!=null)pixelStageOffRoot.gameObject.SetActive(ended);
        foreach(Transform graphic in pixelStageOffOccluders)graphic.gameObject.SetActive(ended);
    }
}
