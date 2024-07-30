using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static AssetBundleBrowser.AssetBundleBuildTab;

namespace HT.Framework.Deployment
{
    /// <summary>
    /// 部署配置 - 检视器
    /// </summary>
    [CustomEditor(typeof(DeploymentConfig))]
    internal sealed class DeploymentConfigInspector : HTFEditor<DeploymentConfig>
    {
        [MenuItem("GameObject/HTFramework/★ Deployment/Config", true)]
        private static bool CreateDeploymentConfigValidate()
        {
            return FindObjectOfType<DeploymentConfig>() == null;
        }
        [MenuItem("GameObject/HTFramework/★ Deployment/Config", false, 200)]
        private static void CreateDeploymentConfig()
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/HTFrameworkDeployment/DeploymentConfig.prefab");
            if (asset)
            {
                GameObject config = PrefabUtility.InstantiatePrefab(asset) as GameObject;
                config.name = "DeploymentConfig";
                config.transform.localPosition = Vector3.zero;
                config.transform.localRotation = Quaternion.identity;
                config.transform.localScale = Vector3.one;
                Selection.activeGameObject = config;
                EditorSceneManager.MarkSceneDirty(config.scene);
                SceneVisibilityManager.instance.Hide(config, true);
                SceneVisibilityManager.instance.DisablePicking(config, true);
            }
            else
            {
                Log.Error("新建部署配置失败，丢失预制体：Assets/HTFrameworkDeployment/DeploymentConfig.prefab");
            }
        }

        private bool _isCanBuild = false;

        protected override bool IsEnableRuntimeData => false;

        protected override void OnDefaultEnable()
        {
            base.OnDefaultEnable();

            _isCanBuild = string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Target.gameObject));
        }
        protected override void OnInspectorDefaultGUI()
        {
            base.OnInspectorDefaultGUI();

            GUILayout.BeginHorizontal();
            Target.ResourcePathType = (DeploymentConfig.PathType)EditorGUILayout.EnumPopup("Local Resource Path", Target.ResourcePathType);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(EditorStyles.textField);
            if (Target.ResourcePathType == DeploymentConfig.PathType.PersistentData)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("PersistentDataPath /", GUILayout.Width(125));
                GUI.color = Color.white;
            }
            else if (Target.ResourcePathType == DeploymentConfig.PathType.StreamingAssets)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("StreamingAssetsPath /", GUILayout.Width(135));
                GUI.color = Color.white;
            }
            Target.LocalResourcePath = EditorGUILayout.TextField(Target.LocalResourcePath, EditorStyles.label);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                Application.OpenURL(Target.LocalResourceFullPath);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Resource Path");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(EditorStyles.textField);
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField("ProjectPath /", GUILayout.Width(80));
            GUI.color = Color.white;
            Target.BuildResourcePath = EditorGUILayout.TextField(Target.BuildResourcePath, EditorStyles.label);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                Application.OpenURL(Target.BuildResourceFullPath);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Remote Resource Path");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(EditorStyles.textField);
            Target.RemoteResourcePath = EditorGUILayout.TextField(Target.RemoteResourcePath, EditorStyles.label);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build New Deployment Version", EditorStyles.largeLabel);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Version", GUILayout.Width(60));
            Target.BuildVersion = EditorGUILayout.TextField(Target.BuildVersion);
            GUI.enabled = _isCanBuild && !string.IsNullOrEmpty(Target.BuildVersion);
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Build " + Target.BuildVersion))
            {
                BuildNewDeploymentVersion(Target.BuildVersion);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (GUI.changed)
            {
                HasChanged();
            }
        }

        /// <summary>
        /// 构建新的部署版本
        /// </summary>
        private void BuildNewDeploymentVersion(string version)
        {
            if (!Directory.Exists(Target.BuildResourceFullPath))
            {
                Directory.CreateDirectory(Target.BuildResourceFullPath);
            }

            string versionPath = $"{Target.BuildResourceFullPath}{version}/";
            if (Directory.Exists(versionPath))
            {
                FileUtil.DeleteFileOrDirectory(versionPath);
            }
            Directory.CreateDirectory(versionPath);

            DeploymentVersion deployment = new DeploymentVersion();
            deployment.Version = version;
            deployment.Date = DateTime.Now.ToString("yyyy.MM.dd");

            #region Deployment Assembly
#if HOTFIX_HybridCLR
            Main main = FindObjectOfType<Main>();
            if (main == null)
            {
                Log.Error($"Build New Deployment Version Error: 请先打开入口场景，并确保将热更新程序集添加到 Main 上！");
                return;
            }
            for (int i = 0; i < main.HotfixAssemblyNames.Length; i++)
            {
                string sourcePath = $"{PathToolkit.ProjectPath}HybridCLRData/HotUpdateDlls/{EditorUserBuildSettings.activeBuildTarget}/{main.HotfixAssemblyNames[i]}.dll";
                string destPath = $"{versionPath}{main.HotfixAssemblyNames[i]}.bytes";
                if (File.Exists(sourcePath))
                {
                    FileUtil.CopyFileOrDirectory(sourcePath, destPath);

                    DeploymentVersion.Assembly assembly = new DeploymentVersion.Assembly();
                    assembly.Name = main.HotfixAssemblyNames[i];
                    assembly.CRC = DeploymentUtility.GetFileMD5(destPath);
                    assembly.Size = DeploymentUtility.GetFileSize(destPath);
                    deployment.Assemblys.Add(assembly);
                }
            }
#endif
            #endregion

            #region Deployment AB
            string dataPath = PathToolkit.ProjectPath + "/Library/AssetBundleBrowserBuild.dat";
            if (File.Exists(dataPath))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream stream = File.Open(dataPath, FileMode.Open);
                BuildTabData data = bf.Deserialize(stream) as BuildTabData;
                stream.Close();
                string abPath = Path.IsPathFullyQualified(data.m_OutputPath) ? data.m_OutputPath : (PathToolkit.ProjectPath + data.m_OutputPath);
                if (Directory.Exists(abPath))
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(abPath);
                    FileInfo[] fileInfos = directoryInfo.GetFiles();
                    for (int i = 0; i < fileInfos.Length; i++)
                    {
                        if (fileInfos[i].FullName.EndsWith(".manifest"))
                        {
                            string name = Path.GetFileNameWithoutExtension(fileInfos[i].FullName);
                            string sourceManifestPath = fileInfos[i].FullName;
                            string destManifestPath = $"{versionPath}{name}.manifest";
                            string sourcePath = sourceManifestPath.Remove(sourceManifestPath.LastIndexOf('.') + 1);
                            string destPath = $"{versionPath}{name}";
                            FileUtil.CopyFileOrDirectory(sourceManifestPath, destManifestPath);
                            FileUtil.CopyFileOrDirectory(sourcePath, destPath);

                            DeploymentVersion.AB ab = new DeploymentVersion.AB();
                            ab.Name = name;
                            ab.CRC = DeploymentUtility.GetCRC(destManifestPath);
                            ab.Size = DeploymentUtility.GetFileSize(destPath);
                            deployment.ABs.Add(ab);
                        }
                    }
                }
            }
            #endregion

            StringBuilder builder = new StringBuilder();
            JsonWriter writer = new JsonWriter(builder);
            writer.PrettyPrint = true;
            writer.IndentValue = 4;
            JsonMapper.ToJson(deployment, writer);
            File.WriteAllText(versionPath + "Version.json", builder.ToString());

            Log.Info($"Build New Deployment Version Succeed: {versionPath.Hyperlink("file:///" + versionPath)}！");
        }
    }
}