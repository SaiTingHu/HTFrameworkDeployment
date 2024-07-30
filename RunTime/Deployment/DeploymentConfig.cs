using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

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
        /// 构建部署版本号【请勿在代码中修改】
        /// </summary>
        [SerializeField] internal string BuildVersion = "v1.0.0";

        private Coroutine _downloadCoroutine;
        private int _startDownloadTime;

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
        /// 更新远端部署资源时的下载信息
        /// </summary>
        public UpdateResourceDownloadInfo DownloadInfo { get; private set; } = new UpdateResourceDownloadInfo();

        protected override void OnDestroy()
        {
            base.OnDestroy();

            UpdateResourceDone(UpdateResourceDownloadInfo.Result.Failure, "Update Resource Error：DeploymentConfig 实例被销毁。");
            StopAllCoroutines();
        }

        /// <summary>
        /// 更新远端部署资源到本地
        /// </summary>
        /// <param name="onUpdating">更新中回调</param>
        public Coroutine UpdateResource(HTFAction<UpdateResourceDownloadInfo> onUpdating = null)
        {
#if UNITY_WEBGL
            Log.Error("Update Resource Error：不支持 WebGL 平台。");
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
            _startDownloadTime = (int)Time.realtimeSinceStartup;

            DownloadInfo.IsDone = false;
            DownloadInfo.DownloadResult = UpdateResourceDownloadInfo.Result.InProgress;

            //建立远端资源版本信息
            string versionStr = null;
            string remoteVersionPath = $"{RemoteResourceFullPath}Version.json";
            yield return ReadRemoteResourceVersion(remoteVersionPath, (str) => { versionStr = str; });
            DeploymentVersion remoteDeployment = JsonToolkit.StringToJson<DeploymentVersion>(versionStr);
            if (remoteDeployment == null || string.IsNullOrEmpty(remoteDeployment.Version))
            {
                UpdateResourceDone(UpdateResourceDownloadInfo.Result.Failure, "Update Resource Error：未获取到远端部署资源的版本信息。");
                yield break;
            }

            //建立本地资源版本信息
            string localVersionPath = $"{LocalResourceFullPath}Version.json";
            versionStr = ReadLocalResourceVersion(localVersionPath);
            DeploymentVersion localDeployment = JsonToolkit.StringToJson<DeploymentVersion>(versionStr);

            //待下载的所有程序集
            List<DeploymentVersion.Assembly> downloadAssemblys = new List<DeploymentVersion.Assembly>();
            //待下载的所有AB包
            List<DeploymentVersion.AB> downloadABs = new List<DeploymentVersion.AB>();

            //本地不存在版本信息，始终下载远端全部资源
            if (localDeployment == null || string.IsNullOrEmpty(localDeployment.Version))
            {
                downloadAssemblys.AddRange(remoteDeployment.Assemblys);
                downloadABs.AddRange(remoteDeployment.ABs);
            }
            //对比本地与远端版本，以增量下载
            else
            {
                //本地与远端版本不一致
                if (remoteDeployment.Version != localDeployment.Version)
                {
                    //对比程序集
                    for (int i = 0; i < remoteDeployment.Assemblys.Count; i++)
                    {
                        DeploymentVersion.Assembly remoteAssembly = remoteDeployment.Assemblys[i];
                        DeploymentVersion.Assembly localAssembly = localDeployment.Assemblys.Find((a) => { return a.Name == remoteAssembly.Name; });
                        //本地不存在，远端存在，为新增的程序集
                        if (localAssembly == null)
                        {
                            downloadAssemblys.Add(remoteAssembly);
                        }
                        else
                        {
                            //本地与远端校验码不同，为修改的程序集
                            if (localAssembly.CRC != remoteAssembly.CRC)
                            {
                                downloadAssemblys.Add(remoteAssembly);
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
                            downloadABs.Add(remoteAB);
                        }
                        else
                        {
                            //本地与远端校验码不同，为修改的AB包
                            if (localAB.CRC != remoteAB.CRC)
                            {
                                downloadABs.Add(remoteAB);
                            }
                        }
                    }
                }
            }

            //记录下载信息
            DownloadInfo.DownloadVersion = remoteDeployment.Version;
            for (int i = 0; i < downloadAssemblys.Count; i++)
            {
                DownloadInfo.TotalDownloadFileNumber += 1;
                DownloadInfo.TotalDownloadFileSize += downloadAssemblys[i].Size;
            }
            for (int i = 0; i < downloadABs.Count; i++)
            {
                //判断本地AB包的CRC，如果与远端的相同，则跳过下载
                string localManifestPath = $"{LocalResourceFullPath}{downloadABs[i].Name}.manifest";
                string localCRC = DeploymentUtility.GetCRC(localManifestPath);
                if (localCRC == downloadABs[i].CRC)
                {
                    downloadABs.RemoveAt(i);
                    i -= 1;
                }
                else
                {
                    DownloadInfo.TotalDownloadFileNumber += 1;
                    DownloadInfo.TotalDownloadFileSize += downloadABs[i].Size;
                }
            }

            //下载程序集
            for (int i = 0; i < downloadAssemblys.Count; i++)
            {
                string remotePath = $"{RemoteResourceFullPath}{downloadAssemblys[i].Name}.bytes";
                string localPath = $"{LocalResourceFullPath}{downloadAssemblys[i].Name}.bytes";
                yield return DownloadFile(remotePath, localPath, downloadAssemblys[i].Size, onUpdating);
            }
            //下载AB包
            for (int i = 0; i < downloadABs.Count; i++)
            {
                //先下载AB包
                string remotePath = $"{RemoteResourceFullPath}{downloadABs[i].Name}";
                string localPath = $"{LocalResourceFullPath}{downloadABs[i].Name}";
                yield return DownloadFile(remotePath, localPath, downloadABs[i].Size, onUpdating);

                //再下载AB包清单文件
                yield return DownloadFile(remotePath + ".manifest", localPath + ".manifest", 0, onUpdating, false);
            }

            //同步远端版本信息到本地
            yield return DownloadFile(remoteVersionPath, localVersionPath, 0, onUpdating, false);

            UpdateResourceDone(UpdateResourceDownloadInfo.Result.Success, null);

#if HOTFIX_HybridCLR && !UNITY_EDITOR
            //自动加载所有 HybridCLR 热更新程序集
            for (int i = 0; i < remoteDeployment.Assemblys.Count; i++)
            {
                string assemblyPath = $"{LocalResourceFullPath}{remoteDeployment.Assemblys[i].Name}.bytes";
                System.Reflection.Assembly.Load(File.ReadAllBytes(assemblyPath));
                ReflectionToolkit.AddRunTimeAssembly(remoteDeployment.Assemblys[i].Name);
            }
#endif

#if HOTFIX_HybridCLR
            Main.Current.HybridCLRCompleted();
#endif
            Main.m_Resource.SetAssetBundlePath(LocalResourceFullPath);
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
        /// 读取远端资源版本信息
        /// </summary>
        /// <param name="path">远端版本文件路径</param>
        /// <param name="onComplete">读取完成回调</param>
        private IEnumerator ReadRemoteResourceVersion(string path, HTFAction<string> onComplete)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    onComplete?.Invoke(null);
                }
            }
        }
        /// <summary>
        /// 读取本地资源版本信息
        /// </summary>
        /// <param name="path">本地版本文件路径</param>
        /// <returns>版本信息</returns>
        private string ReadLocalResourceVersion(string path)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
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
                    UpdateResourceDone(UpdateResourceDownloadInfo.Result.Failure, $"Update Resource Error：{request.error}");
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