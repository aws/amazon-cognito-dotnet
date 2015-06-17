using Amazon.CognitoIdentity;
using Amazon.CognitoSync.SyncManager.Internal;
using Amazon.SecurityToken;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace Amazon.CognitoSync.SyncManager
{
    /// <summary>
    /// The SQLiteCognitoAWSCredentials extends from <see cref="Amazon.CognitoIdentity.CognitoAWSCredentials"/> 
    /// and adds support for caching of identity id using SQLite
    /// </summary>
    public class SQLiteCognitoAWSCredentials : CognitoAWSCredentials
    {

        /// <summary>
        /// Constructs a new SQLiteCognitoAWSCredentials instance, which will use the
        /// specified Amazon Cognito identity pool to get short lived session credentials.
        /// </summary>
        /// <param name="identityPoolId">The Amazon Cogntio identity pool to use</param>
        /// <param name="region">Region to use when accessing Amazon Cognito and AWS Security Token Service.</param>
        public SQLiteCognitoAWSCredentials(string identityPoolId, RegionEndpoint region)
            : base(identityPoolId, region) { }


        /// <summary>
        /// Constructs a new SQLiteCognitoAWSCredentials instance, which will use the
        /// specified Amazon Cognito identity pool to make a requests to the
        /// AWS Security Token Service (STS) to request short lived session credentials.
        /// </summary>
        /// <param name="accountId">The AWS accountId for the account with Amazon Cognito</param>
        /// <param name="identityPoolId">The Amazon Cogntio identity pool to use</param>
        /// <param name="unAuthRoleArn">The ARN of the IAM Role that will be assumed when unauthenticated</param>
        /// <param name="authRoleArn">The ARN of the IAM Role that will be assumed when authenticated</param>
        /// <param name="region">Region to use when accessing Amazon Cognito and AWS Security Token Service.</param>
        public SQLiteCognitoAWSCredentials(string accountId, string identityPoolId, string unAuthRoleArn, string authRoleArn, RegionEndpoint region)
            : base(accountId, identityPoolId, unAuthRoleArn, authRoleArn, region) { }

        /// <summary>
        /// Constructs a new SQLiteCognitoAWSCredentials instance, which will use the
        /// specified Amazon Cognito identity pool to make a requests to the
        /// AWS Security Token Service (STS) to request short lived session credentials.
        /// </summary>
        /// <param name="accountId">The AWS accountId for the account with Amazon Cognito</param>
        /// <param name="identityPoolId">The Amazon Cogntio identity pool to use</param>
        /// <param name="unAuthRoleArn">The ARN of the IAM Role that will be assumed when unauthenticated</param>
        /// <param name="authRoleArn">The ARN of the IAM Role that will be assumed when authenticated</param>
        /// <param name="cibClient">Preconfigured Cognito Identity client to make requests with</param>
        /// <param name="stsClient">>Preconfigured STS client to make requests with</param>
        public SQLiteCognitoAWSCredentials(string accountId, string identityPoolId, string unAuthRoleArn, string authRoleArn, IAmazonCognitoIdentity cibClient, IAmazonSecurityTokenService stsClient)
            : base(accountId, identityPoolId, unAuthRoleArn, authRoleArn, cibClient, stsClient) { }


        private static readonly String IDENTITY_ID_CACHE_KEY = "CognitoIdentity:IdentityId";

        public override void CacheIdentityId(string identityId)
        {
            base.CacheIdentityId(identityId);
            using (var kvStore = new SQLiteLocalStorage())
            {
                kvStore.CacheIdentity(GetNamespacedKey(IDENTITY_ID_CACHE_KEY), identityId);
            }
        }

        public override void ClearIdentityCache()
        {
            base.ClearIdentityCache();
            using (var kvStore = new SQLiteLocalStorage())
            {
                kvStore.DeleteCachedIdentity(GetNamespacedKey(IDENTITY_ID_CACHE_KEY));
            }
        }

        public override string GetCachedIdentityId()
        {
            string identityId = null;
            using (var kvStore = new SQLiteLocalStorage())
            {
                identityId = kvStore.GetIdentity(GetNamespacedKey(IDENTITY_ID_CACHE_KEY));
            }
            return identityId;
        }

    }

}
