using System.Collections.Generic;

namespace HT.Framework.Deployment
{
    /// <summary>
    /// 检测远端部署资源时的信息
    /// </summary>
    public class CheckResourceInfo
    {
        /// <summary>
        /// 版本已改变
        /// </summary>
        public bool IsVersionChanged { get; internal set; }
        /// <summary>
        /// 远端版本号
        /// </summary>
        public string Version { get; internal set; }
        /// <summary>
        /// 远端发行日志
        /// </summary>
        public string ReleaseNotes { get; internal set; }
        /// <summary>
        /// 待下载的所有补充元数据
        /// </summary>
        public List<DeploymentVersion.Metadata> DownloadMetadatas { get; internal set; } = new List<DeploymentVersion.Metadata>();
        /// <summary>
        /// 待下载的所有程序集
        /// </summary>
        public List<DeploymentVersion.Assembly> DownloadAssemblys { get; internal set; } = new List<DeploymentVersion.Assembly>();
        /// <summary>
        /// 待下载的所有AB包
        /// </summary>
        public List<DeploymentVersion.AB> DownloadABs { get; internal set; } = new List<DeploymentVersion.AB>();
        /// <summary>
        /// 待下载的文件数量
        /// </summary>
        public int TotalDownloadFileNumber { get; internal set; }
        /// <summary>
        /// 待下载的文件总大小（单位：KB）
        /// </summary>
        public int TotalDownloadFileSize { get; internal set; }

        /// <summary>
        /// 重置
        /// </summary>
        internal void Reset()
        {
            IsVersionChanged = false;
            Version = null;
            ReleaseNotes = null;
            DownloadMetadatas.Clear();
            DownloadAssemblys.Clear();
            DownloadABs.Clear();
            TotalDownloadFileNumber = 0;
            TotalDownloadFileSize = 0;
        }
    }
}