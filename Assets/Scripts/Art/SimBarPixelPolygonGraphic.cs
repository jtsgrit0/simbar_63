using UnityEngine;
using UnityEngine.UI;

/// <summary>Reuses the unchanged room pixels through a polygon for foreground occlusion.</summary>
public sealed class SimBarPixelPolygonGraphic : MaskableGraphic
{
    private Texture image;
    private Vector2[] points;
    public override Texture mainTexture => image != null ? image : s_WhiteTexture;

    public void Initialize(Texture texture, Vector2[] polygon)
    {
        image=texture; points=polygon; raycastTarget=false;
        RectTransform rect=rectTransform;
        rect.anchorMin=rect.anchorMax=new Vector2(0f,1f);
        rect.pivot=new Vector2(0f,1f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(960f,558f);
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();if(points==null||points.Length<3)return;
        // Ear clipping also supports the non-convex lower drum-kit silhouette.
        var indices=new System.Collections.Generic.List<int>();
        for(int i=0;i<points.Length;i++)
        {
            Vector2 p=points[i];vertices.AddVert(new Vector3(p.x,-p.y,0f),color,new Vector2(p.x/960f,1f-p.y/558f));indices.Add(i);
        }
        float area=0f;for(int i=0;i<points.Length;i++){Vector2 a=points[i],b=points[(i+1)%points.Length];area+=a.x*b.y-b.x*a.y;}
        float sign=area>=0f?1f:-1f;
        int remaining=points.Length*points.Length;
        while(indices.Count>2&&remaining-->0)
        {
            bool clipped=false;
            for(int n=0;n<indices.Count;n++)
            {
                int a=indices[(n+indices.Count-1)%indices.Count],b=indices[n],c=indices[(n+1)%indices.Count];
                if(Cross(points[b]-points[a],points[c]-points[b])*sign<=.001f)continue;
                bool occupied=false;
                foreach(int p in indices)
                    if(p!=a&&p!=b&&p!=c&&Inside(points[p],points[a],points[b],points[c],sign)){occupied=true;break;}
                if(occupied)continue;
                vertices.AddTriangle(a,b,c);indices.RemoveAt(n);clipped=true;break;
            }
            if(!clipped)break;
        }
    }
    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    private static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c,float sign)=>
        Cross(b-a,p-a)*sign>=0f&&Cross(c-b,p-b)*sign>=0f&&Cross(a-c,p-c)*sign>=0f;
}
