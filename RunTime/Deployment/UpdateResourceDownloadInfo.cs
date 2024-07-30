namespace HT.Framework.Deployment
{
    /// <summary>
    /// 更新远端部署资源时的下载信息
    /// </summary>
    public class UpdateResourceDownloadInfo
    {
        /// <summary>
        /// 是否下载完成
        /// </summary>
        public bool IsDone { get; internal set; }
        /// <summary>
        /// 下载状态
        /// </summary>
        public Result DownloadResult { get; internal set; }
        /// <summary>
        /// 如果下载失败，获取的错误提示
        /// </summary>
        public string Error { get; internal set; }
        /// <summary>
        /// 下载的资源版本
        /// </summary>
        public string DownloadVersion { get; internal set; }
        /// <summary>
        /// 需下载的文件数量
        /// </summary>
        public int TotalDownloadFileNumber { get; internal set; }
        /// <summary>
        /// 已下载的文件数量
        /// </summary>
        public int DownloadedFileNumber { get; internal set; }
        /// <summary>
        /// 需下载的文件总大小（单位：KB）
        /// </summary>
        public int TotalDownloadFileSize { get; internal set; }
        /// <summary>
        /// 已下载的文件总大小（单位：KB）
        /// </summary>
        public int DownloadedFileSize { get; internal set; }
        /// <summary>
        /// 已下载持续时长（单位：s）
        /// </summary>
        public int DownloadedTime { get; internal set; }
        /// <summary>
        /// 实时下载速度（单位：KB/s）
        /// </summary>
        public int DownloadedSpeed { get; internal set; }
        
        /// <summary>
        /// 重置
        /// </summary>
        internal void Reset()
        {
            IsDone = false;
            DownloadResult = Result.InProgress;
            Error = null;
            DownloadVersion = null;
            TotalDownloadFileNumber = 0;
            DownloadedFileNumber = 0;
            TotalDownloadFileSize = 0;
            DownloadedFileSize = 0;
            DownloadedTime = 0;
            DownloadedSpeed = 0;
        }

        /// <summary>
        /// 结果
        /// </summary>
        public enum Result
        {
            /// <summary>
            /// 进行中
            /// </summary>
            InProgress,
            /// <summary>
            /// 成功
            /// </summary>
            Success,
            /// <summary>
            /// 失败
            /// </summary>
            Failure
        }
    }
}