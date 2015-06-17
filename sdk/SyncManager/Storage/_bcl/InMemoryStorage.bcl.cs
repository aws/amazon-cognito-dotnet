using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amazon.CognitoSync.SyncManager.Internal
{
    public partial class InMemoryStorage
    {

        static IDictionary<string, string> kvStore = new Dictionary<string, string>();

        #region BCL Specific implementation for identityId caching

        public void CacheIdentity(string key, string identity)
        {
            kvStore.Add(key, identity);
        }

        public string GetIdentity(string key)
        {
            string identityId = null;
            kvStore.TryGetValue(key, out identityId);
            return identityId;
        }

        public void DeleteCachedIdentity(string key)
        {
            kvStore.Remove(key);
        }

        #endregion
    }
}
