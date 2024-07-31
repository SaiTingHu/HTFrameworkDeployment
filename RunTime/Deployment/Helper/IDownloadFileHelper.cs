using UnityEngine.Networking;

namespace HT.Framework.Deployment
{
    /// <summary>
    /// 下载文件助手
    /// </summary>
    public interface IDownloadFileHelper
    {
        /// <summary>
        /// 下载文件之前
        /// </summary>
        /// <param name="request">下载文件的请求</param>
        void OnBeforeDownload(UnityWebRequest request);
    }
}