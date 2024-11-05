using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
#if HOTFIX_HybridCLR && HybridCLR_5
using HybridCLR;
#endif

[assembly: InternalsVisibleTo("HTFramework.Deployment.Editor")]

namespace HT.Framework.Deployment
{
    /// <summary>
    /// 部署配置
    /// </summary>
    [DisallowMultipleComponent]
    [LockTransform]
    public sealed class DeploymentConfig : SingletonBehaviourBase<DeploymentConfig>
    {
        /// <summary>
        /// 下载文件助手的类型【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal string HelperType = "<None>";
        /// <summary>
        /// 本地部署资源根路径【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal string LocalResourcePath = "LocalResource/";
        /// <summary>
        /// 构建部署资源根路径【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal string BuildResourcePath = "BuildResource/";
        /// <summary>
        /// 远端部署资源根路径【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal string RemoteResourcePath;
        /// <summary>
        /// 资源根路径类型【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal PathType ResourcePathType = PathType.PersistentData;
        /// <summary>
        /// 是否自动加载热更程序集【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal bool IsAutoLoadAssembly = false;
        /// <summary>
        /// 构建部署版本号【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal string BuildVersion = "v1.0.0";

        private IDownloadFileHelper _helper;
        private Coroutine _downloadCoroutine;
        private int _startDownloadTime;
        private DeploymentVersion _localVersion;

        /// <summary>
        /// 下载文件助手
        /// </summary>
        public IDownloadFileHelper Helper
        {
            get
            {
                if (_helper == null)
                {
                    if (HelperType != "<None>")
                    {
                        Type type = ReflectionToolkit.GetTypeInRunTimeAssemblies(HelperType, false);
                        if (type != null)
                        {
                            if (typeof(IDownloadFileHelper).IsAssignableFrom(type))
                            {
                                _helper = Activator.CreateInstance(type) as IDownloadFileHelper;
                            }
                            else
                            {
                                Log.Error($"创建下载文件助手失败：下载文件助手类 {HelperType} 必须实现接口 IDownloadFileHelper ！");
                            }
                        }
                        else
                        {
                            Log.Error($"创建下载文件助手失败：丢失下载文件助手类 {HelperType} ！");
                        }
                    }
                }
                return _helper;
            }
        }
        /// <summary>
        /// 本地部署资源根路径
        /// </summary>
        public string LocalResourceFullPath
        {
            get
            {
                if (ResourcePathType == PathType.PersistentData)
                {
                    return $"{Application.persistentDataPath}/{LocalResourcePath}";
                }
                else if (ResourcePathType == PathType.StreamingAssets)
                {
                    return $"{Application.streamingAssetsPath}/{LocalResourcePath}";
                }
                else
                {
                    return LocalResourcePath;
                }
            }
        }
        /// <summary>
        /// 构建部署资源根路径
        /// </summary>
        public string BuildResourceFullPath
        {
            get
            {
                return $"{PathToolkit.ProjectPath}{BuildResourcePath}";
            }
        }
        /// <summary>
        /// 远端部署资源根路径
        /// </summary>
        public string RemoteResourceFullPath
        {
            get
            {
                return RemoteResourcePath;
            }
        }
        /// <summary>
        /// 检测远端部署资源时的信息
        /// </summary>
        public CheckResourceInfo CheckInfo { get; private set; } = new CheckResourceInfo();
        /// <summary>
        /// 更新远端部署资源时的下载信息
        /// </summary>
        public UpdateResourceDownloadInfo DownloadInfo { get; private set; } = new UpdateResourceDownloadInfo();
        /// <summary>
        /// 本地版本信息
        /// </summary>
        public DeploymentVersion LocalVersion
        {
            get
            {
                if (_localVersion == null)
                {
                    string versionStr = ReadLocalResource("/Version.json");
                    _localVersion = JsonToolkit.StringToJson<DeploymentVersion>(versionStr);
                }
                return _localVersion;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            UpdateResourceDone(UpdateResourceDownloadInfo.Result.Failure, "Deployment Error：DeploymentConfig 实例被销毁。");
            StopAllCoroutines();
        }

        /// <summary>
        /// 检测远端部署资源
        /// </summary>
        /// <param name="onCheckEnd">检测结束回调</param>
        public Coroutine CheckResource(HTFAction<CheckResourceInfo> onCheckEnd = null)
        {
#if UNITY_WEBGL
            Log.Error("Deployment Error：不支持 WebGL 平台。");
            return null;
#endif
            CheckInfo.Reset();
            return StartCoroutine(CheckResourceCoroutine(onCheckEnd));
        }
        /// <summary>
        /// 检测远端部署资源
        /// </summary>
        private IEnumerator CheckResourceCoroutine(HTFAction<CheckResourceInfo> onCheckEnd)
        {
            //建立远端资源版本信息
            string versionStr = null;
            yield return ReadRemoteResource("/Version.json", (str) => { versionStr = str; });
            DeploymentVersion remoteDeployment = JsonToolkit.StringToJson<DeploymentVersion>(versionStr);
            if (remoteDeployment == null || string.IsNullOrEmpty(remoteDeployment.Version))
            {
                Log.Error("Deployment Error：未获取到远端部署资源的版本信息。");
                onCheckEnd?.Invoke(CheckInfo);
                yield break;
            }

            //建立本地资源版本信息
            versionStr = ReadLocalResource("/Version.json");
            DeploymentVersion localDeployment = JsonToolkit.StringToJson<DeploymentVersion>(versionStr);

            //本地不存在版本信息，始终下载远端全部资源
            if (localDeployment == null || string.IsNullOrEmpty(localDeployment.Version))
            {
                CheckInfo.IsVersionChanged = true;
                CheckInfo.DownloadMetadatas.AddRange(remoteDeployment.Metadatas);
                CheckInfo.DownloadAssemblys.AddRange(remoteDeployment.Assemblys);
                CheckInfo.DownloadABs.AddRange(remoteDeployment.ABs);
            }
            //本地存在版本信息，对比本地与远端版本，以增量下载
            else
            {
                //本地与远端版本不一致
                if (remoteDeployment.Version != localDeployment.Version)
                {
                    CheckInfo.IsVersionChanged = true;
                    //对比补充元数据
                    for (int i = 0; i < remoteDeployment.Metadatas.Count; i++)
                    {
                        DeploymentVersion.Metadata remoteMetadata = remoteDeployment.Metadatas[i];
                        DeploymentVersion.Metadata localMetadata = localDeployment.Metadatas.Find((m) => { return m.Name == remoteMetadata.Name; });
                        //本地不存在，远端存在，为新增的补充元数据
                        if (localMetadata == null)
                        {
                            CheckInfo.DownloadMetadatas.Add(remoteMetadata);
                        }
                        else
                        {
                            //本地与远端校验码不同，为修改的补充元数据
                            if (localMetadata.CRC != remoteMetadata.CRC)
                            {
                                CheckInfo.DownloadMetadatas.Add(remoteMetadata);
                            }
                        }
                    }
                    //对比程序集
                    for (int i = 0; i < remoteDeployment.Assemblys.Count; i++)
                    {
                        DeploymentVersion.Assembly remoteAssembly = remoteDeployment.Assemblys[i];
                        DeploymentVersion.Assembly localAssembly = localDeployment.Assemblys.Find((a) => { return a.Name == remoteAssembly.Name; });
                        //本地不存在，远端存在，为新增的程序集
                        if (localAssembly == null)
                        {
                            CheckInfo.DownloadAssemblys.Add(remoteAssembly);
                        }
                        else
                        {
                            //本地与远端校验码不同，为修改的程序集
                            if (localAssembly.CRC != remoteAssembly.CRC)
                            {
                                CheckInfo.DownloadAssemblys.Add(remoteAssembly);
                            }
                        }
                    }
                    //对比AB包
                    for (int i = 0; i < remoteDeployment.ABs.Count; i++)
                    {
                        DeploymentVersion.AB remoteAB = remoteDeployment.ABs[i];
                        DeploymentVersion.AB localAB = localDeployment.ABs.Find((a) => { return a.Name == remoteAB.Name; });
                        //本地不存在，远端存在，为新增的AB包
                        if (localAB == null)
                        {
                            CheckInfo.DownloadABs.Add(remoteAB);
                        }
                        else
                        {
                            //本地与远端校验码不同，为修改的AB包
                            if (localAB.CRC != remoteAB.CRC)
                            {
                                CheckInfo.DownloadABs.Add(remoteAB);
                            }
                        }
                    }
                }
                else
                {
                    CheckInfo.IsVersionChanged = false;
                }
            }

            //记录待下载信息
            if (CheckInfo.IsVersionChanged)
            {
                CheckInfo.Version = remoteDeployment.Version;
                yield return ReadRemoteResource("/ReleaseNotes.txt", (str) => { CheckInfo.ReleaseNotes = str; });

                for (int i = 0; i < CheckInfo.DownloadMetadatas.Count; i++)
                {
                    CheckInfo.TotalDownloadFileNumber += 1;
                    CheckInfo.TotalDownloadFileSize += CheckInfo.DownloadMetadatas[i].Size;
                }
                for (int i = 0; i < CheckInfo.DownloadAssemblys.Count; i++)
                {
                    CheckInfo.TotalDownloadFileNumber += 1;
                    CheckInfo.TotalDownloadFileSize += CheckInfo.DownloadAssemblys[i].Size;
                }
                for (int i = 0; i < CheckInfo.DownloadABs.Count; i++)
                {
                    //判断本地AB包的CRC，如果与远端的相同，则跳过下载
                    string localManifestPath = $"{LocalResourceFullPath}{CheckInfo.DownloadABs[i].Name}.manifest";
                    string localCRC = DeploymentUtility.GetCRC(localManifestPath);
                    if (localCRC == CheckInfo.DownloadABs[i].CRC)
                    {
                        CheckInfo.DownloadABs.RemoveAt(i);
                        i -= 1;
                    }
                    else
                    {
                        CheckInfo.TotalDownloadFileNumber += 1;
                        CheckInfo.TotalDownloadFileSize += CheckInfo.DownloadABs[i].Size;
                    }
                }
            }

            onCheckEnd?.Invoke(CheckInfo);
        }

        /// <summary>
        /// 更新远端部署资源到本地
        /// </summary>
        /// <param name="onUpdating">更新中回调</param>
        public Coroutine UpdateResource(HTFAction<UpdateResourceDownloadInfo> onUpdating = null)
        {
#if UNITY_WEBGL
            Log.Error("Deployment Error：不支持 WebGL 平台。");
            return null;
#endif
            DownloadInfo.Reset();
            _downloadCoroutine = StartCoroutine(UpdateResourceCoroutine(onUpdating));
            return _downloadCoroutine;
        }
        /// <summary>
        /// 更新远端部署资源到本地 - 开始
        /// </summary>
        private IEnumerator UpdateResourceCoroutine(HTFAction<UpdateResourceDownloadInfo> onUpdating)
        {
            if (!Directory.Exists(LocalResourceFullPath))
            {
                Directory.CreateDirectory(LocalResourceFullPath);
            }

            _startDownloadTime = (int)Time.realtimeSinceStartup;

            DownloadInfo.IsDone = false;
            DownloadInfo.DownloadResult = UpdateResourceDownloadInfo.Result.InProgress;

            if (CheckInfo.IsVersionChanged)
            {
                DownloadInfo.DownloadVersion = CheckInfo.Version;
                DownloadInfo.TotalDownloadFileNumber = CheckInfo.TotalDownloadFileNumber;
                DownloadInfo.TotalDownloadFileSize = CheckInfo.TotalDownloadFileSize;

                //下载补充元数据
                for (int i = 0; i < CheckInfo.DownloadMetadatas.Count; i++)
                {
                    string remotePath = $"{RemoteResourceFullPath}{CheckInfo.DownloadMetadatas[i].Name}.metadata";
                    string localPath = $"{LocalResourceFullPath}{CheckInfo.DownloadMetadatas[i].Name}.metadata";
                    yield return DownloadFile(remotePath, localPath, CheckInfo.DownloadMetadatas[i].Size, onUpdating);
                }
                //下载程序集
                for (int i = 0; i < CheckInfo.DownloadAssemblys.Count; i++)
                {
                    string remotePath = $"{RemoteResourceFullPath}{CheckInfo.DownloadAssemblys[i].Name}.bytes";
                    string localPath = $"{LocalResourceFullPath}{CheckInfo.DownloadAssemblys[i].Name}.bytes";
                    yield return DownloadFile(remotePath, localPath, CheckInfo.DownloadAssemblys[i].Size, onUpdating);
                }
                //下载AB包
                for (int i = 0; i < CheckInfo.DownloadABs.Count; i++)
                {
                    //先下载AB包
                    string remotePath = $"{RemoteResourceFullPath}{CheckInfo.DownloadABs[i].Name}";
                    string localPath = $"{LocalResourceFullPath}{CheckInfo.DownloadABs[i].Name}";
                    yield return DownloadFile(remotePath, localPath, CheckInfo.DownloadABs[i].Size, onUpdating);

                    //再下载AB包清单文件
                    yield return DownloadFile(remotePath + ".manifest", localPath + ".manifest", 0, onUpdating, false);
                }

                //下载资源定位文件
                yield return DownloadFile($"{RemoteResourceFullPath}ResourceLocation.loc", $"{LocalResourceFullPath}ResourceLocation.loc", 0, onUpdating, false);

                //同步远端版本信息到本地
                yield return DownloadFile($"{RemoteResourceFullPath}Version.json", $"{LocalResourceFullPath}Version.json", 0, onUpdating, false);
            }
            else
            {
                onUpdating?.Invoke(DownloadInfo);
            }

            //启用 HybridCLR 热更新，自动补充元数据、加载热更程序集
#if HOTFIX_HybridCLR && HybridCLR_5 && !UNITY_EDITOR
            //自动为 HybridCLR 补充元数据
            for (int i = 0; i < Main.Current.MetadataNames.Length; i++)
            {
                string metadataPath = $"{LocalResourceFullPath}{Main.Current.MetadataNames[i]}.metadata";
                LoadImageErrorCode code = RuntimeApi.LoadMetadataForAOTAssembly(File.ReadAllBytes(metadataPath), HomologousImageMode.SuperSet);
                Debug.Log($"Load Metadata For AOT Assembly：{Main.Current.MetadataNames[i]}, Result：{code}.");
            }
            //自动加载所有 HybridCLR 热更新程序集
            for (int i = 0; i < Main.Current.HotfixAssemblyNames.Length; i++)
            {
                string assemblyPath = $"{LocalResourceFullPath}{Main.Current.HotfixAssemblyNames[i]}.bytes";
                System.Reflection.Assembly.Load(File.ReadAllBytes(assemblyPath));
                ReflectionToolkit.AddRunTimeAssembly(Main.Current.HotfixAssemblyNames[i]);
                Debug.Log($"Load Hotfix Assembly：{Main.Current.HotfixAssemblyNames[i]}.");
            }
#endif

            //未启用 HybridCLR 热更新，使用源生程序集热更方式
#if HOTFIX_HybridCLR && !HybridCLR_5
            //自动加载所有热更新程序集
            if (IsAutoLoadAssembly)
            {
                for (int i = 0; i < Main.Current.HotfixAssemblyNames.Length; i++)
                {
                    string assemblyPath = $"{LocalResourceFullPath}{Main.Current.HotfixAssemblyNames[i]}.bytes";
                    System.Reflection.Assembly.Load(File.ReadAllBytes(assemblyPath));
                    ReflectionToolkit.AddRunTimeAssembly(Main.Current.HotfixAssemblyNames[i]);
                    Debug.Log($"Load Hotfix Assembly：{Main.Current.HotfixAssemblyNames[i]}.");
                }
            }
#endif

#if HOTFIX_HybridCLR
            Main.Current.HybridCLRCompleted();
#endif
            Main.m_Resource.SetAssetBundlePath(LocalResourceFullPath);

            UpdateResourceDone(UpdateResourceDownloadInfo.Result.Success, null);
        }
        /// <summary>
        /// 更新远端部署资源到本地 - 完成
        /// </summary>
        /// <param name="result">更新结果</param>
        /// <param name="error">如果更新失败，错误提示</param>
        private void UpdateResourceDone(UpdateResourceDownloadInfo.Result result, string error)
        {
            DownloadInfo.IsDone = true;
            DownloadInfo.DownloadResult = result;
            DownloadInfo.Error = error;

            if (_downloadCoroutine != null)
            {
                StopCoroutine(_downloadCoroutine);
                _downloadCoroutine = null;
            }
        }

        /// <summary>
        /// 读取远端资源文本信息
        /// </summary>
        /// <param name="path">远端文件路径</param>
        /// <param name="onComplete">读取完成回调</param>
        public IEnumerator ReadRemoteResource(string path, HTFAction<string> onComplete)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(RemoteResourceFullPath + path))
            {
                Helper?.OnBeforeDownload(request);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    onComplete?.Invoke(null);
                    Log.Error($"Deployment Error：{path}, {request.error}");
                }
            }
        }
        /// <summary>
        /// 读取本地资源文本信息
        /// </summary>
        /// <param name="path">本地文件路径</param>
        /// <returns>文本信息</returns>
        public string ReadLocalResource(string path)
        {
            if (File.Exists(LocalResourceFullPath + path))
            {
                return File.ReadAllText(LocalResourceFullPath + path);
            }
            return null;
        }

        /// <summary>
        /// 下载文件到本地
        /// </summary>
        /// <param name="remotePath">远端文件路径</param>
        /// <param name="localPath">本地保存路径</param>
        /// <param name="fileSize">文件大小</param>
        /// <param name="onLoading">下载中回调</param>
        /// <param name="isRecordInfo">是否记录下载信息</param>
        private IEnumerator DownloadFile(string remotePath, string localPath, int fileSize, HTFAction<UpdateResourceDownloadInfo> onLoading, bool isRecordInfo = true)
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }

            int downloadedFileSize = DownloadInfo.DownloadedFileSize;
            using (UnityWebRequest request = UnityWebRequest.Get(remotePath))
            {
                request.downloadHandler = new DownloadHandlerFile(localPath);
                Helper?.OnBeforeDownload(request);
                request.SendWebRequest();
                while (!request.isDone)
                {
                    if (isRecordInfo)
                    {
                        DownloadInfo.DownloadedTime = (int)(Time.realtimeSinceStartup - _startDownloadTime);
                        if (DownloadInfo.DownloadedTime <= 0) DownloadInfo.DownloadedTime = 1;
                        DownloadInfo.DownloadedFileSize = downloadedFileSize + (int)(fileSize * request.downloadProgress);
                        DownloadInfo.DownloadedSpeed = DownloadInfo.DownloadedFileSize / DownloadInfo.DownloadedTime;
                        onLoading?.Invoke(DownloadInfo);
                    }
                    yield return null;
                }
                if (isRecordInfo)
                {
                    DownloadInfo.DownloadedFileNumber += 1;
                    DownloadInfo.DownloadedFileSize = downloadedFileSize + fileSize;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    UpdateResourceDone(UpdateResourceDownloadInfo.Result.Failure, $"Deployment Error：{request.error}");
                }
            }
        }

        /// <summary>
        /// 资源根路径类型
        /// </summary>
        public enum PathType
        {
            PersistentData,
            StreamingAssets,
            Other
        }
    }
}