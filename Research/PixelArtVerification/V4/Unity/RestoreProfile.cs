UnityEditor.AssetDatabase.ImportAsset("Assets/Settings/Build Profiles/Web - Desktop - Release.asset",UnityEditor.ImportAssetOptions.ForceUpdate);
return new {runInBackground=UnityEditor.PlayerSettings.runInBackground,sceneDirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,playing=UnityEditor.EditorApplication.isPlaying};
