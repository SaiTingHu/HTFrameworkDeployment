using System.Collections.Generic;

namespace HT.Framework.Deployment
{
    /// <summary>
    /// 部署的一个版本
    /// </summary>
    public sealed class DeploymentVersion
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public string Version;
        /// <summary>
        /// 部署日期
        /// </summary>
        public string Date;
        /// <summary>
        /// 部署的所有热更新程序集
        /// </summary>
        public List<Assembly> Assemblys = new List<Assembly>();
        /// <summary>
        /// 部署的所有AB包
        /// </summary>
        public List<AB> ABs = new List<AB>();

        /// <summary>
        /// 部署的热更新程序集
        /// </summary>
        public sealed class Assembly
        {
            /// <summary>
            /// 名称
            /// </summary>
            public string Name;
            /// <summary>
            /// 校验码
            /// </summary>
            public string CRC;
            /// <summary>
            /// 大小（KB）
            /// </summary>
            public int Size;
        }
        /// <summary>
        /// 部署的AB包
        /// </summary>
        public sealed class AB
        {
            /// <summary>
            /// 名称
            /// </summary>
            public string Name;
            /// <summary>
            /// 校验码
            /// </summary>
            public string CRC;
            /// <summary>
            /// 大小（KB）
            /// </summary>
            public int Size;
        }
    }
}