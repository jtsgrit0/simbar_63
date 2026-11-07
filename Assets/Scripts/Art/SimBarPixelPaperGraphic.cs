using UnityEngine;
using UnityEngine.UI;

/// <summary>One pooled UI mesh for a colored paper square or a connected tissue ribbon.</summary>
public sealed class SimBarPixelPaperGraphic : MaskableGraphic
{
    private bool tissue,airborne;
    private int index;
    private float age,phase;
    public void SetPaper(bool isTissue,bool isAirborne,int variant,float seconds=0f,float offset=0f)
    {
        if(tissue==isTissue&&airborne==isAirborne&&index==variant&&age==seconds&&phase==offset)return;
        tissue=isTissue;airborne=isAirborne;index=variant;age=seconds;phase=offset;SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        if(airborne)
        {
            if(tissue)
                for(int segment=0;segment<5+index%4;segment++)
                    Quad(vertices,Mathf.Floor(Mathf.Sin(phase+age*5f+segment*.85f)*2.5f+.5f),segment*2f,3f,2f,segment%3==2?new Color32(214,206,224,255):(Color32)color);
            else
            {
                int turn=Mathf.FloorToInt(age*8f+phase)%3;
                Quad(vertices,0f,0f,turn==1?2f:turn==2?4f:3f,turn==1?4f:turn==2?2f:3f,color);
            }
            return;
        }
        Quad(vertices,-1f,2f,tissue?11f:6f,1f,new Color32(16,23,35,153));
        if(tissue)
        {
            Quad(vertices,0f,0f,4f,2f,color);Quad(vertices,3f,1f,4f,2f,color);Quad(vertices,6f,0f,3f,2f,color);
            Quad(vertices,1f,0f,3f,1f,new Color32(255,247,232,255));
        }
        else Quad(vertices,0f,0f,index%3==1?2f:4f,index%3==1?4f:2f,color);
    }
    private static void Quad(VertexHelper vertices,float x,float y,float width,float height,Color32 color)
    {
        int start=vertices.currentVertCount;
        vertices.AddVert(new Vector3(x,-y),color,Vector2.zero);vertices.AddVert(new Vector3(x+width,-y),color,Vector2.zero);
        vertices.AddVert(new Vector3(x+width,-y-height),color,Vector2.zero);vertices.AddVert(new Vector3(x,-y-height),color,Vector2.zero);
        vertices.AddTriangle(start,start+1,start+2);vertices.AddTriangle(start,start+2,start+3);
    }
}
