
using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.CognitoSync;
using Amazon.CognitoSync.SyncManager;
using AWSSDK_DotNet.IntegrationTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Collections.Generic;
using Amazon.Runtime;
using System.IO;
using System.Data.SQLite;
using System.Threading;

namespace AWSSDK_DotNet.IntegrationTests.Tests
{
    [TestClass]
    public class SyncManager
    {

        //identity related components
        public static int MaxResults = 15;

        private List<string> allPoolIds = new List<string>();
        private const PoolRoles poolRoles = PoolRoles.Unauthenticated | PoolRoles.Authenticated;

        [Flags]
        enum PoolRoles
        {
            None = 0,
            Authenticated = 1,
            Unauthenticated = 2
        }

        // Facebook information required to run Facebook tests
        public const string FacebookAppId = "809434219126221";
        public const string FacebookAppSecret = "76cd5a3ad8681a5418a438c48fdecd7b";
        private const string FacebookProvider = "graph.facebook.com";
        FacebookUtilities.FacebookCreateUserResponse facebookUser = null;

        private static RegionEndpoint TEST_REGION = RegionEndpoint.USEast1;

        private List<string> roleNames = new List<string>();
        private const string policyName = "TestPolicy";

        string poolid = null;
        string poolName = null;

        internal const string DB_FILE_NAME = "aws_cognito_sync.db";

        [TestCleanup]
        public void Cleanup()
        {
            if (poolid != null)
                DeleteIdentityPool(poolid);

            CleanupCreatedRoles();

            if (facebookUser != null)
                FacebookUtilities.DeleteFacebookUser(facebookUser);

            //drop all the tables from the db
            using (SQLiteConnection connection = new SQLiteConnection(string.Format("Data Source={0};Version=3;", DB_FILE_NAME)))
            {
                connection.Open();

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE records";
                    cmd.ExecuteNonQuery();
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE datasets";
                    cmd.ExecuteNonQuery();
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE kvstore";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        [TestMethod]
        [TestCategory("SyncManager")]
        public void AuthenticatedCredentialsTest()
        {
            CognitoAWSCredentials authCred = AuthCredentials;

            string identityId = authCred.GetIdentityId();
            Assert.IsTrue(!string.IsNullOrEmpty(identityId));
            ImmutableCredentials cred = authCred.GetCredentials();
            Assert.IsNotNull(cred);
        }


        [TestMethod]
        [TestCategory("SyncManager")]
        public void DatasetLocalStorageTest()
        {
            {
                using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
                {
                    syncManager.WipeData();
                    Dataset d = syncManager.OpenOrCreateDataset("testDataset");
                    d.Put("testKey", "testValue");
                }
            }
            {
                using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
                {
                    Dataset d = syncManager.OpenOrCreateDataset("testDataset");
                    Assert.AreEqual("testValue", d.Get("testKey"));
                }
            }
        }

        // <summary>
        /// Test case: Store a value in a dataset and sync it. Wipe all local data.
        /// After synchronizing the dataset we should have our stored value back.
        /// </summary>
        [TestMethod]
        [TestCategory("SyncManager")]
        public void DatasetCloudStorageTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                Thread.Sleep(2000);
                using (Dataset d = syncManager.OpenOrCreateDataset("testDataset2"))
                {
                    d.Put("key", "he who must not be named");

                    d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                    {
                        d.ClearAllDelegates();
                        string erasedValue = d.Get("key");
                        syncManager.WipeData();
                        d.OnSyncSuccess += delegate(object sender2, SyncSuccessEvent e2)
                        {
                            string restoredValues = d.Get("key");
                            Assert.IsNotNull(erasedValue);
                            Assert.IsNotNull(restoredValues);
                            Assert.AreEqual(erasedValue, restoredValues);
                        };

                        d.Synchronize();
                    };
                    d.OnSyncFailure += delegate(object sender, SyncFailureEvent e)
                    {
                        Console.WriteLine(e.Exception.Message);
                        Console.WriteLine(e.Exception.StackTrace);
                        Assert.Fail("sync failed");
                    };
                    d.OnSyncConflict += (Dataset dataset, List<SyncConflict> conflicts) =>
                    {
                        Assert.Fail();
                        return false;
                    };
                    d.OnDatasetMerged += (Dataset dataset, List<string> datasetNames) =>
                    {
                        Assert.Fail();
                        return false;
                    };
                    d.OnDatasetDeleted += (Dataset dataset) =>
                    {
                        Assert.Fail();
                        return false;
                    };
                    d.Synchronize();
                }
            }
        }

        /// <summary>
        /// Test Case: 
        /// </summary>
        [TestMethod]
        [TestCategory("SyncManager")]
        public void MergeTest()
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string uniqueName = ((DateTime.UtcNow - epoch).TotalSeconds).ToString();
            string uniqueName2 = uniqueName + "_";

            UnAuthCredentials.Clear();

            using (CognitoSyncManager sm1 = new CognitoSyncManager(AuthCredentials))
            {
                sm1.WipeData();
                Thread.Sleep(2000);
                using (Dataset d = sm1.OpenOrCreateDataset("test"))
                {
                    d.Put(uniqueName, uniqueName);
                    d.OnSyncSuccess += delegate(object s1, SyncSuccessEvent e1)
                    {
                        UnAuthCredentials.Clear();

                        using (CognitoSyncManager sm2 = new CognitoSyncManager(UnAuthCredentials))
                        {
                            Thread.Sleep(2000);
                            using (Dataset d2 = sm2.OpenOrCreateDataset("test"))
                            {
                                d2.Put(uniqueName2, uniqueName2);
                                d2.OnSyncSuccess += delegate(object s2, SyncSuccessEvent e2)
                                {
                                    AuthCredentials.Clear();
                                    UnAuthCredentials.Clear();
                                    //now we will use auth credentials.
                                    using (CognitoSyncManager sm3 = new CognitoSyncManager(AuthCredentials))
                                    {
                                        Thread.Sleep(2000);
                                        using (Dataset d3 = sm3.OpenOrCreateDataset("test"))
                                        {
                                            bool mergeTriggered = false;
                                            d3.OnSyncSuccess += (object sender, SyncSuccessEvent e) =>
                                            {
                                                if (!mergeTriggered)
                                                    Assert.Fail("Expecting DatasetMerged instead of OnSyncSuccess");
                                            };
                                            d3.OnSyncConflict += (Dataset dataset, List<SyncConflict> syncConflicts) =>
                                            {
                                                Assert.Fail();
                                                return false;
                                            };
                                            d3.OnDatasetDeleted += (Dataset dataset) =>
                                            {
                                                Assert.Fail();
                                                return false;
                                            };
                                            d3.OnDatasetMerged += (Dataset ds, List<string> datasetNames) =>
                                            {
                                                mergeTriggered = true;
                                                datasetNames.ForEach((mergeds) =>
                                                {
                                                    Dataset mergedDataset = sm3.OpenOrCreateDataset(mergeds);
                                                    mergedDataset.Delete();
                                                    mergedDataset.Synchronize();
                                                });
                                                return true;
                                            };
                                            d3.Synchronize();
                                        }
                                    }
                                };
                                d2.OnSyncFailure += (object sender, SyncFailureEvent e) =>
                                {
                                    Console.WriteLine(e.Exception.Message);
                                    Console.WriteLine(e.Exception.StackTrace); 
                                    Assert.Fail();
                                };
                                d2.OnSyncConflict += (Dataset dataset, List<SyncConflict> conflicts) =>
                                {
                                    Assert.Fail();
                                    return false;
                                };
                                d2.OnDatasetDeleted += (Dataset dataset) =>
                                {
                                    Assert.Fail();
                                    return false;
                                };
                                d2.OnDatasetMerged += (Dataset dataset, List<string> datasetNames) =>
                                {
                                    Assert.Fail();
                                    return false;
                                };
                                d2.Synchronize();
                            }
                        }
                    };
                    d.OnSyncFailure += delegate(object s, SyncFailureEvent e)
                    {
                        Console.WriteLine(e.Exception.Message);
                        Console.WriteLine(e.Exception.StackTrace);
                        Assert.Fail("Sync Failed");
                    };
                    d.OnSyncConflict += (Dataset dataset, List<SyncConflict> syncConflicts) =>
                    {
                        Assert.Fail();
                        return false;
                    };
                    d.OnDatasetDeleted += (Dataset dataset) =>
                    {
                        Assert.Fail();
                        return false;
                    };
                    d.OnDatasetMerged += (Dataset dataset, List<string> datasetNames) =>
                    {
                        Assert.Fail();
                        return false;
                    };
                    d.Synchronize();
                }
            }
        }

        /// <summary>
        /// Test case: Check that the dataset metadata is modified appropriately when calling Synchronize.
        /// We test for the dirty bit, the sync count and the last modified timmestamp.
        /// </summary>
        [TestMethod]
        [TestCategory("SyncManager")]
        public void MetadataTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                using (Dataset d = syncManager.OpenOrCreateDataset("testDataset3"))
                {
                    d.Put("testKey3", "the initial value");

                    //Initial properties
                    var records = d.GetAllRecords();
                    Record r = d.GetAllRecords()[records.Count - 1];
                    long initialSyncCount = r.SyncCount;
                    bool initialDirty = r.IsModified;
                    DateTime initialDate = r.DeviceLastModifiedDate.Value;

                    d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                    {
                        //Properties after Synchronize
                        Record r2 = d.GetAllRecords()[records.Count - 1];
                        long synchronizedSyncCount = r2.SyncCount;
                        bool synchronizedDirty = r2.IsModified;
                        DateTime synchronizedDate = r2.DeviceLastModifiedDate.Value;

                        d.Put("testKey3", "a new value");

                        //Properties after changing the content again
                        Record r3 = d.GetAllRecords()[records.Count - 1];
                        long finalSyncCount = r3.SyncCount;
                        bool finalDirty = r3.IsModified;
                        DateTime finalDate = r3.DeviceLastModifiedDate.Value;

                        Assert.IsTrue(initialDirty);
                        Assert.IsTrue(!synchronizedDirty);
                        Assert.IsTrue(finalDirty);

                        Assert.IsTrue(synchronizedSyncCount > initialSyncCount);
                        Assert.IsTrue(synchronizedSyncCount == finalSyncCount);

                        Assert.IsTrue(finalDate > initialDate);
                        Assert.IsTrue(initialDate == synchronizedDate);
                    };
                    d.Synchronize();
                }
            }
        }


        /// <summary>
        /// Test case: Produce a conflict and check that SyncConflict is triggered.
        /// Also check that by returning false in SyncConflict, the Synchronize operation
        /// is aborted and nothing else gets called. 
        /// </summary>
        [TestMethod]
        [TestCategory("SyncManager")]
        public void ConflictTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                using (Dataset d = syncManager.OpenOrCreateDataset("testDataset3"))
                {
                    d.Put("testKey3", "the initial value");
                    d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                    {
                        d.ClearAllDelegates();
                        syncManager.WipeData();
                        using (Dataset d2 = syncManager.OpenOrCreateDataset("testDataset3"))
                        {
                            bool conflictTriggered = false;
                            d2.Put("testKey3", "a different value");

                            d2.OnSyncConflict += delegate(Dataset dataset, List<SyncConflict> conflicts)
                            {
                                conflictTriggered = true;
                                return false;
                            };
                            d2.OnSyncSuccess += delegate(object sender4, SyncSuccessEvent e4)
                            {
                                Assert.Fail("Expecting OnSyncConflict instead of OnSyncSuccess");
                            };
                            d2.OnSyncFailure += delegate(object sender4, SyncFailureEvent e4)
                            {
                                Assert.IsTrue(conflictTriggered, "Expecting OnSyncConflict instead of OnSyncFailure");
                            };
                            d2.Synchronize();
                        }
                    };
                    d.Synchronize();
                }
            }
        }

        /// <summary>
        /// Test case: Produce a conflict and check that the three ways provided by the SDK
        /// for resolving a conflict (local wins, remote wins, and override) work. We also check
        /// that returning true in SyncConflict allows the Synchronization operationn to continue.
        /// </summary>
        [TestMethod]
        [TestCategory("SyncManager")]
        public void ResolveConflictTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials))
            {
                syncManager.WipeData();
                using (Dataset d = syncManager.OpenOrCreateDataset("testDataset4"))
                {
                    d.Put("a", "1");
                    d.Put("b", "2");
                    d.Put("c", "3");
                    d.OnSyncSuccess += delegate(object sender, SyncSuccessEvent e)
                    {
                        d.ClearAllDelegates();
                        syncManager.WipeData();
                        using (Dataset d2 = syncManager.OpenOrCreateDataset("testDataset4"))
                        {
                            d2.Put("a", "10");
                            d2.Put("b", "20");
                            d2.Put("c", "30");

                            bool resolved = false;
                            d2.OnSyncConflict += delegate(Dataset dataset, List<SyncConflict> conflicts)
                            {
                                List<Amazon.CognitoSync.SyncManager.Record> resolvedRecords = new List<Amazon.CognitoSync.SyncManager.Record>();
                                int i = 0;
                                foreach (SyncConflict conflictRecord in conflicts)
                                {
                                    if (i == 0) resolvedRecords.Add(conflictRecord.ResolveWithLocalRecord());
                                    else if (i == 1) resolvedRecords.Add(conflictRecord.ResolveWithValue("42"));
                                    else resolvedRecords.Add(conflictRecord.ResolveWithRemoteRecord());
                                    i++;
                                }
                                dataset.Resolve(resolvedRecords);
                                resolved = true;
                                return true;
                            };
                            d2.OnSyncSuccess += delegate(object sender4, SyncSuccessEvent e4)
                            {
                                if (resolved)
                                {
                                    Assert.AreSame(d2.Get("a"), "10");
                                    Assert.AreSame(d2.Get("b"), "42");
                                    Assert.AreSame(d2.Get("c"), "3");
                                }
                                else
                                {
                                    Assert.Fail("Expecting SyncConflict instead of SyncSuccess");
                                }

                            };
                            d2.OnSyncFailure += delegate(object sender4, SyncFailureEvent e4)
                            {
                                Assert.Fail("Expecting SyncConflict instead of SyncFailure");
                            };
                            d2.Synchronize();
                        }
                    };
                    d.Synchronize();
                }
            }
        }


        private CognitoAWSCredentials _AuthCredentials;
        private CognitoAWSCredentials _UnauthCredentials;

        private CognitoAWSCredentials AuthCredentials
        {
            get
            {
                if (_AuthCredentials != null)
                    return _AuthCredentials;

                if (poolid == null)
                {
                    CreateIdentityPool(out poolid, out poolName);
                    Thread.Sleep(2000);
                }

                facebookUser = FacebookUtilities.CreateFacebookUser(FacebookAppId, FacebookAppSecret);

                _AuthCredentials = new SQLiteCognitoAWSCredentials(poolid, TEST_REGION);
                _AuthCredentials.AddLogin(FacebookProvider, facebookUser.AccessToken);

                //create facebook token
                return _AuthCredentials;
            }
        }


        private CognitoAWSCredentials UnAuthCredentials
        {
            get
            {
                if (_UnauthCredentials != null)
                    return _UnauthCredentials;

                if (poolid == null)
                    CreateIdentityPool(out poolid, out poolName);

                _UnauthCredentials = new SQLiteCognitoAWSCredentials(poolid, TEST_REGION);
                return _UnauthCredentials;
            }
        }

        IAmazonCognitoIdentity _client;

        private IAmazonCognitoIdentity Client
        {
            get
            {
                if (_client == null) _client = new AmazonCognitoIdentityClient(new StoredProfileAWSCredentials());
                return _client;
            }
        }

        private void CreateIdentityPool(out string poolId, out string poolName)
        {
            poolName = "netTestPool" + DateTime.Now.ToFileTime();
            var request = new CreateIdentityPoolRequest
            {
                IdentityPoolName = poolName,
                AllowUnauthenticatedIdentities = true,
                SupportedLoginProviders = new Dictionary<string, string>() { { FacebookProvider, FacebookAppId } }
            };

            var createPoolResult = Client.CreateIdentityPool(request);
            Assert.IsNotNull(createPoolResult.IdentityPoolId);
            Assert.IsNotNull(createPoolResult.IdentityPoolName);
            Assert.AreEqual(request.AllowUnauthenticatedIdentities, createPoolResult.AllowUnauthenticatedIdentities);
            poolId = createPoolResult.IdentityPoolId;
            allPoolIds.Add(poolId);

            var describePoolResult = Client.DescribeIdentityPool(new DescribeIdentityPoolRequest
            {
                IdentityPoolId = poolId
            });
            Assert.AreEqual(poolId, describePoolResult.IdentityPoolId);
            Assert.AreEqual(poolName, describePoolResult.IdentityPoolName);

            var getIdentityPoolRolesResult = Client.GetIdentityPoolRoles(poolId);
            Assert.AreEqual(poolId, getIdentityPoolRolesResult.IdentityPoolId);
            Assert.AreEqual(0, getIdentityPoolRolesResult.Roles.Count);

            var roles = new Dictionary<string, string>(StringComparer.Ordinal);
            if ((poolRoles & PoolRoles.Unauthenticated) == PoolRoles.Unauthenticated)
                roles["unauthenticated"] = PrepareRole();
            if ((poolRoles & PoolRoles.Authenticated) == PoolRoles.Authenticated)
                roles["authenticated"] = PrepareRole();

            Client.SetIdentityPoolRoles(new SetIdentityPoolRolesRequest
            {
                IdentityPoolId = poolId,
                Roles = roles
            });

            getIdentityPoolRolesResult = Client.GetIdentityPoolRoles(poolId);
            Assert.AreEqual(poolId, getIdentityPoolRolesResult.IdentityPoolId);
            Assert.AreEqual(NumberOfPoolRoles, getIdentityPoolRolesResult.Roles.Count);

            Thread.Sleep(2000);
        }


        private IEnumerable<IdentityPoolShortDescription> GetAllPoolsHelper()
        {
            var request = new ListIdentityPoolsRequest { MaxResults = MaxResults };
            ListIdentityPoolsResponse result;
            do
            {
                result = Client.ListIdentityPools(request);
                foreach (var pool in result.IdentityPools)
                {
                    Assert.IsNotNull(pool);
                    Assert.IsFalse(string.IsNullOrEmpty(pool.IdentityPoolId));
                    Assert.IsFalse(string.IsNullOrEmpty(pool.IdentityPoolName));
                    yield return pool;
                }

                request.NextToken = result.NextToken;
            } while (!string.IsNullOrEmpty(result.NextToken));
        }

        private IEnumerable<IdentityDescription> GetAllIdentitiesHelper(string poolId)
        {
            var request = new ListIdentitiesRequest
            {
                MaxResults = MaxResults,
                IdentityPoolId = poolId
            };
            ListIdentitiesResponse result;
            do
            {
                result = Client.ListIdentities(request);
                foreach (var ident in result.Identities)
                {
                    Assert.IsNotNull(ident);
                    Assert.IsFalse(string.IsNullOrEmpty(ident.IdentityId));
                    Assert.IsNotNull(ident.Logins);
                    yield return ident;
                }
                request.NextToken = result.NextToken;
            } while (!string.IsNullOrEmpty(result.NextToken));
        }


        public List<IdentityPoolShortDescription> GetAllPools()
        {
            return GetAllPoolsHelper().ToList();
        }

        public List<IdentityDescription> GetAllIdentities(string poolId)
        {
            return GetAllIdentitiesHelper(poolId).ToList();
        }

        public void DeleteIdentityPool(string poolId)
        {
            if (!string.IsNullOrEmpty(poolId))
            {
                var allPools = GetAllPools();
                var pool = allPools.SingleOrDefault(p => string.Equals(poolId, p.IdentityPoolId, StringComparison.Ordinal));

                if (pool != null)
                {
                    Console.WriteLine("Found pool with id [{0}], deleting", poolId);

                    try
                    {
                        Client.DeleteIdentityPool(new DeleteIdentityPoolRequest
                        {
                            IdentityPoolId = poolId
                        });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("failed to delete [{0}]", poolId);
                        Console.WriteLine("exception e" + e.Message);
                        Console.WriteLine(e.StackTrace);
                    }
                }
            }
        }

        private int NumberOfPoolRoles
        {
            get
            {
                int count = 0;
                if ((poolRoles & PoolRoles.Authenticated) == PoolRoles.Authenticated)
                    count++;
                if ((poolRoles & PoolRoles.Unauthenticated) == PoolRoles.Unauthenticated)
                    count++;
                return count;
            }
        }

        public void UpdateIdentityPool(string poolId, string poolName, Dictionary<string, string> providers)
        {
            var updateRequest = new UpdateIdentityPoolRequest
            {
                IdentityPoolName = poolName,
                IdentityPoolId = poolId,
                AllowUnauthenticatedIdentities = true,
            };
            if (providers != null && providers.Count > 0)
                updateRequest.SupportedLoginProviders = providers;

            Client.UpdateIdentityPool(updateRequest);
        }


        public string PrepareRole()
        {
            // Assume role policy which accepts OAuth tokens from Google, Facebook or Cognito, and allows AssumeRoleWithWebIdentity action.
            string assumeRolePolicy = @"{
    ""Version"":""2012-10-17"",
    ""Statement"":[
        {
            ""Effect"":""Allow"",
            ""Principal"":{
                ""Federated"":[""accounts.google.com"",""graph.facebook.com"", ""cognito-identity.amazonaws.com""]
            },
            ""Action"":[""sts:AssumeRoleWithWebIdentity""]
        }
    ]
}";
            // Role policy that allows all operations for a number of services
            var allowPolicy = @"{
    ""Version"" : ""2012-10-17"",
    ""Statement"" : [
        {
            ""Effect"" : ""Allow"",
            ""Action"" : [
                ""ec2:*"",
                ""iam:*"",
                ""cloudwatch:*"",
                ""cognito-identity:*"",
                ""cognito-sync:*"",
                ""s3:*""
            ],
            ""Resource"" : ""*""
        }
    ]
}";
            string roleArn;
            using (var identityClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient())
            {
                string roleName = "NetWebIdentityRole" + new Random().Next();
                var response = identityClient.CreateRole(new Amazon.IdentityManagement.Model.CreateRoleRequest
                {
                    AssumeRolePolicyDocument = assumeRolePolicy,
                    RoleName = roleName
                });

                identityClient.PutRolePolicy(new Amazon.IdentityManagement.Model.PutRolePolicyRequest
                {
                    PolicyDocument = allowPolicy,
                    PolicyName = policyName,
                    RoleName = response.Role.RoleName
                });

                roleArn = response.Role.Arn;
                roleNames.Add(roleName);
            }

            return roleArn;
        }

        public void CleanupCreatedRoles()
        {
            foreach (var roleName in roleNames)
            {
                DeleteRole(roleName);
            }
            roleNames.Clear();
        }

        private void DeleteRole(string roleName)
        {
            using (var identityClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient())
            {
                identityClient.DeleteRolePolicy(new Amazon.IdentityManagement.Model.DeleteRolePolicyRequest
                {
                    PolicyName = policyName,
                    RoleName = roleName
                });

                identityClient.DeleteRole(new Amazon.IdentityManagement.Model.DeleteRoleRequest
                {
                    RoleName = roleName
                });
            }
        }



    }
}