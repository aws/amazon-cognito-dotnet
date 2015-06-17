using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amazon.CognitoSync.SyncManager
{
    public partial interface ILocalStorage
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="identity"></param>
        void CacheIdentity(string key, string identity);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string GetIdentity(string key);



        void DeleteCachedIdentity(string key);
    }
}
