using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class PixelSocialRigQA
{
 public static object Main()
 {
  var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Social rig check");SceneManager.MoveGameObjectToScene(root,scene);
  try{
   var visual=SimBarCharacterVisual.Create("singer",root.transform,Vector3.zero,Color.magenta).GetComponent<SimBarCharacterVisual>();visual.SetManaged(true);
   var bones=(Dictionary<string,Transform>)typeof(SimBarCharacterVisual).GetField("bones",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(visual);
   visual.ApplyOffStagePresentation(0f,0f,"highfive",0f);var before=bones["handr"].position;
   visual.ApplyOffStagePresentation(.5f,0f,"highfive",.5f);var after=bones["handr"].position;
   var shoulder=bones["upperarmr"].position;var elbow=bones["lowerarmr"].position;
   return new {before=before.ToString("F4"),after=after.ToString("F4"),shoulder=shoulder.ToString("F4"),elbow=elbow.ToString("F4"),delta=Vector3.Distance(before,after),upperLength=Vector3.Distance(shoulder,elbow),lowerLength=Vector3.Distance(elbow,after)};
  }finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
}
