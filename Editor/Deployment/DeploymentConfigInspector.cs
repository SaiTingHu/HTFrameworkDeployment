using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using static AssetBundleBrowser.AssetBundleBuildTab;

namespace HT.Framework.Deployment
{
    /// <summary>
    /// 部署配置 - 检视器
    /// </summary>
    [CustomEditor(typeof(DeploymentConfig))]
    [GithubURL("https://github.com/SaiTingHu/HTFrameworkDeployment")]
    [CSDNBlogURL("https://wanderer.blog.csdn.net/article/details/140823964")]
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

        private List<Type> _types;
        private bool _isCanBuild = false;
        private BuildTarget _buildTarget = BuildTarget.Android;

        protected override bool IsEnableRuntimeData => false;

        protected override void OnDefaultEnable()
        {
            base.OnDefaultEnable();

            _types = ReflectionToolkit.GetTypesInAllAssemblies(type =>
            {
                return typeof(IDownloadFileHelper).IsAssignableFrom(type) && typeof(IDownloadFileHelper) != type;
            }, false);
            _isCanBuild = string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Target.gameObject));
        }
        protected override void OnInspectorDefaultGUI()
        {
            base.OnInspectorDefaultGUI();

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !EditorApplication.isPlaying && _types.Count > 0;
            EditorGUILayout.LabelField("Download File Helper", GUILayout.Width(LabelWidth));
            if (GUILayout.Button(Target.HelperType, EditorStyles.popup, GUILayout.Width(EditorGUIUtility.currentViewWidth - LabelWidth - 25)))
            {
                GenericMenu gm = new GenericMenu();
                gm.AddItem(new GUIContent("<None>"), Target.HelperType == "<None>", () =>
                {
                    Undo.RecordObject(target, "Change Download File Helper");
                    Target.HelperType = "<None>";
                    HasChanged();
                });
                for (int i = 0; i < _types.Count; i++)
                {
                    int j = i;
                    gm.AddItem(new GUIContent(_types[j].FullName), Target.HelperType == _types[j].FullName, () =>
                    {
                        Undo.RecordObject(target, "Change Download File Helper");
                        Target.HelperType = _types[j].FullName;
                        HasChanged();
                    });
                }
                gm.ShowAsContext();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

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

#if HOTFIX_HybridCLR && !HybridCLR_5
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            GUILayout.Label("Compile Originating DLL", EditorStyles.largeLabel);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            GUILayout.Label("Platform", GUILayout.Width(60));
            _buildTarget = (BuildTarget)EditorGUILayout.EnumPopup(_buildTarget);
            GUI.color = Color.white;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Compile"))
            {
                if (EditorUtility.DisplayDialog("Compile Originating DLL", "是否确认开始编译源生程序集？", "是的", "我再想想"))
                {
                    CompileOriginatingDLL();
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            Target.IsAutoLoadAssembly = EditorGUILayout.Toggle("Auto Load Assembly", Target.IsAutoLoadAssembly);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
#endif
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            GUILayout.Label("Build New Deployment Version", EditorStyles.largeLabel);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            GUILayout.Label("Version", GUILayout.Width(60));
            Target.BuildVersion = EditorGUILayout.TextField(Target.BuildVersion);
            GUI.color = Color.white;
            GUI.enabled = _isCanBuild && !string.IsNullOrEmpty(Target.BuildVersion);
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Build " + Target.BuildVersion))
            {
                if (EditorUtility.DisplayDialog("Build New Deployment Version", $"是否确认构建一个新的资源版本 [{Target.BuildVersion}]？如果该版本已存在，将被覆盖。", "是的", "我再想想"))
                {
                    BuildNewDeploymentVersion(Target.BuildVersion);
                }
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
        /// 编译源生程序集
        /// </summary>
        private void CompileOriginatingDLL()
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(_buildTarget);
            ScriptCompilationSettings scriptCompilationSettings = new ScriptCompilationSettings();
            scriptCompilationSettings.group = group;
            scriptCompilationSettings.target = _buildTarget;
            PlayerBuildInterface.CompilePlayerScripts(scriptCompilationSettings, Target.BuildResourceFullPath + "ScriptAssemblies");

            EditorUtility.ClearProgressBar();
            Log.Info("Deployment：编译源生程序集完成！");
        }
        /// <summary>
        /// 构建新的部署版本
        /// </summary>
        private void BuildNewDeploymentVersion(string version)
        {
            EditorUtility.DisplayProgressBar("Build New Deployment Version", $"Build Version {version} ......", 0);

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

            Main main = FindObjectOfType<Main>();
            if (main == null)
            {
                Log.Error($"Build New Deployment Version Error: 请先打开入口场景，并确保将热更新程序集添加到 Main 上！");
                EditorUtility.ClearProgressBar();
                return;
            }

            #region Deployment Metadata
#if HOTFIX_HybridCLR && HybridCLR_5
            for (int i = 0; i < main.MetadataNames.Length; i++)
            {
                string sourcePath = $"{PathToolkit.ProjectPath}HybridCLRData/AssembliesPostIl2CppStrip/{EditorUserBuildSettings.activeBuildTarget}/{main.MetadataNames[i]}.dll";
                string destPath = $"{versionPath}{main.MetadataNames[i]}.metadata";
                if (File.Exists(sourcePath))
                {
                    FileUtil.CopyFileOrDirectory(sourcePath, destPath);

                    DeploymentVersion.Metadata metadata = new DeploymentVersion.Metadata();
                    metadata.Name = main.MetadataNames[i];
                    metadata.CRC = DeploymentUtility.GetFileMD5(destPath);
                    metadata.Size = DeploymentUtility.GetFileSize(destPath);
                    deployment.Metadatas.Add(metadata);
                }
            }
#endif
            #endregion

            #region Deployment Assembly
#if HOTFIX_HybridCLR && HybridCLR_5
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

#if HOTFIX_HybridCLR && !HybridCLR_5
            for (int i = 0; i < main.HotfixAssemblyNames.Length; i++)
            {
                string sourcePath = $"{Target.BuildResourceFullPath}ScriptAssemblies/{main.HotfixAssemblyNames[i]}.dll";
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
            string dataPath = PathToolkit.ProjectPath + "Library/AssetBundleBrowserBuild.dat";
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

            #region Deployment Resource Location
            StringBuilder resourceLocation = new StringBuilder();
            string[] abNames = AssetDatabase.GetAllAssetBundleNames();
            for (int i = 0; i < abNames.Length; i++)
            {
                string abName = abNames[i];
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(abName);
                for (int j = 0; j < assetPaths.Length; j++)
                {
                    resourceLocation.Append($"{abName}|{assetPaths[j]}\r\n");
                }
            }
            File.WriteAllText(versionPath + "ResourceLocation.loc", resourceLocation.ToString());
            #endregion

            #region Deployment Release Notes
            File.WriteAllText(versionPath + "ReleaseNotes.txt", "");
            #endregion

            StringBuilder builder = new StringBuilder();
            JsonWriter writer = new JsonWriter(builder);
            writer.PrettyPrint = true;
            writer.IndentValue = 4;
            JsonMapper.ToJson(deployment, writer);
            File.WriteAllText(versionPath + "Version.json", builder.ToString());

            Log.Info($"Build New Deployment Version Succeed: {versionPath.Hyperlink("file:///" + versionPath)}！");
            Log.Info("请自行编辑此版本的发行日志：ReleaseNotes.txt。");
            EditorUtility.ClearProgressBar();
        }
    }
}