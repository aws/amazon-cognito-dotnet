using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.CognitoSync;
using CommonTests.Framework;
using CommonTests.IntegrationTests.Utils;
using NUnit.Framework;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using SQLitePCL;
using Amazon.Util.Internal.PlatformServices;
using System.IO;
using Amazon.Runtime;
using Amazon.CognitoSync.SyncManager;

namespace CommonTests.IntegrationTests.SyncManager
{
    public class SyncManagerTests : TestBase<AmazonCognitoSyncClient>
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
        public const string FacebookAppId = "";
        public const string FacebookAppSecret = "";
        private const string FacebookProvider = "graph.facebook.com";
        static FacebookUtilities.FacebookCreateUserResponse facebookUser = null;

        private static RegionEndpoint TEST_REGION = RegionEndpoint.USEast1;

        private static List<string> roleNames = new List<string>();
        private const string policyName = "TestPolicy";

        static string poolid = null;
        static string poolName = null;

        internal const string DB_FILE_NAME = "aws_cognito_sync.db";

        #region tear down

        [OneTimeTearDown]
        public void Cleanup()
        {
            RunAsSync(async () =>
            {

                var applicationInfo = ServiceFactory.Instance.GetService<IApplicationInfo>();
                string dbPath = Path.Combine(applicationInfo.SpecialFolder, DB_FILE_NAME);

                if (poolid != null)
                    await DeleteIdentityPool(poolid).ConfigureAwait(false);

                await CleanupCreatedRoles().ConfigureAwait(false);

                if (facebookUser != null)
                    await FacebookUtilities.DeleteFacebookUserAsync(facebookUser).ConfigureAwait(false);

                if (_AuthCredentials != null)
                    _AuthCredentials.Clear();

                if (_UnauthCredentials != null)
                    _UnauthCredentials.Clear();

                //drop all the tables from the db
                using (SQLiteConnection connection = new SQLiteConnection(dbPath))
                {
                    using (var sqliteStatement = connection.Prepare("DROP TABLE IF EXISTS records"))
                    {
                        var result = sqliteStatement.Step();
                    }

                    using (var sqliteStatement = connection.Prepare("DROP TABLE IF EXISTS datasets"))
                    {
                        var result = sqliteStatement.Step();
                    }
                }
            });
        }

        #endregion

        #region test cases

        [Test(TestOf = typeof(AmazonCognitoSyncClient))]
        public void AuthenticatedCredentialsTest()
        {
            RunAsSync(async () =>
            {
                CognitoAWSCredentials authCred = AuthCredentials;

                string identityId = await authCred.GetIdentityIdAsync().ConfigureAwait(false);
                Assert.IsTrue(!string.IsNullOrEmpty(identityId));
                ImmutableCredentials cred = await authCred.GetCredentialsAsync().ConfigureAwait(false);
                Assert.IsNotNull(cred);
            });
        }


        [Test(TestOf = typeof(AmazonCognitoSyncClient))]
        public void DatasetLocalStorageTest()
        {
            {
                using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials, TestRunner.RegionEndpoint))
                {
                    syncManager.WipeData();
                    Dataset d = syncManager.OpenOrCreateDataset("testDataset");
                    d.Put("testKey", "testValue");
                }
            }
            {
                using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials, TestRunner.RegionEndpoint))
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
        [Test(TestOf = typeof(AmazonCognitoSyncClient))]
        public void DatasetCloudStorageTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials, TestRunner.RegionEndpoint))
            {
                syncManager.WipeData();
                //Thread.Sleep(2000);
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

                        RunAsSync(async () => await d.SynchronizeAsync());
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
                    RunAsSync(async () => await d.SynchronizeAsync());
                }
            }
        }

        /// <summary>
        /// Test Case: 
        /// </summary>
        [Test(TestOf = typeof(AmazonCognitoSyncClient))]
        public void MergeTest()
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string uniqueName = ((DateTime.UtcNow - epoch).TotalSeconds).ToString();
            string uniqueName2 = uniqueName + "_";

            UnAuthCredentials.Clear();

            using (CognitoSyncManager sm1 = new CognitoSyncManager(AuthCredentials, TestRunner.RegionEndpoint))
            {
                sm1.WipeData();
                //Thread.Sleep(2000);
                using (Dataset d = sm1.OpenOrCreateDataset("test"))
                {
                    d.Put(uniqueName, uniqueName);
                    d.OnSyncSuccess += delegate(object s1, SyncSuccessEvent e1)
                    {
                        UnAuthCredentials.Clear();

                        using (CognitoSyncManager sm2 = new CognitoSyncManager(UnAuthCredentials, TestRunner.RegionEndpoint))
                        {
                            //Thread.Sleep(2000);
                            using (Dataset d2 = sm2.OpenOrCreateDataset("test"))
                            {
                                d2.Put(uniqueName2, uniqueName2);
                                d2.OnSyncSuccess += delegate(object s2, SyncSuccessEvent e2)
                                {
                                    AuthCredentials.Clear();
                                    UnAuthCredentials.Clear();
                                    //now we will use auth credentials.
                                    using (CognitoSyncManager sm3 = new CognitoSyncManager(AuthCredentials, TestRunner.RegionEndpoint))
                                    {
                                        //Thread.Sleep(2000);
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
                                                foreach (var mergeds in datasetNames)
                                                {
                                                    Dataset mergedDataset = sm3.OpenOrCreateDataset(mergeds);
                                                    mergedDataset.Delete();
                                                    RunAsSync(async () => await mergedDataset.SynchronizeAsync());
                                                };
                                                return true;
                                            };
                                            RunAsSync(async () => await d3.SynchronizeAsync());
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
                                RunAsSync(async () => await d2.SynchronizeAsync());
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
                    RunAsSync(async () => await d.SynchronizeAsync());
                }
            }
        }

        /// <summary>
        /// Test case: Check that the dataset metadata is modified appropriately when calling Synchronize.
        /// We test for the dirty bit, the sync count and the last modified timmestamp.
        /// </summary>
        [Test(TestOf = typeof(AmazonCognitoSyncClient))]
        public void MetadataTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials, TestRunner.RegionEndpoint))
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
                    RunAsSync(async () => await d.SynchronizeAsync());
                }
            }
        }


        /// <summary>
        /// Test case: Produce a conflict and check that SyncConflict is triggered.
        /// Also check that by returning false in SyncConflict, the Synchronize operation
        /// is aborted and nothing else gets called. 
        /// </summary>
        [Test(TestOf = typeof(AmazonCognitoSyncClient))]
        public void ConflictTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials, TestRunner.RegionEndpoint))
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
                            RunAsSync(async () => await d2.SynchronizeAsync());
                        }
                    };
                    RunAsSync(async () => await d.SynchronizeAsync());
                }
            }
        }

        /// <summary>
        /// Test case: Produce a conflict and check that the three ways provided by the SDK
        /// for resolving a conflict (local wins, remote wins, and override) work. We also check
        /// that returning true in SyncConflict allows the Synchronization operationn to continue.
        /// </summary>
        [Test(TestOf = typeof(AmazonCognitoSyncClient))]
        public void ResolveConflictTest()
        {
            using (CognitoSyncManager syncManager = new CognitoSyncManager(UnAuthCredentials, TestRunner.RegionEndpoint))
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
                            RunAsSync(async () => await d2.SynchronizeAsync());
                        }
                    };
                    RunAsSync(async () => await d.SynchronizeAsync());
                }
            }
        }
        #endregion


        #region helper api's
        private static CognitoAWSCredentials _AuthCredentials;
        private static CognitoAWSCredentials _UnauthCredentials;

        private CognitoAWSCredentials AuthCredentials
        {
            get
            {
                if (_AuthCredentials != null)
                    return _AuthCredentials;

                if (poolid == null)
                {
                    CreateIdentityPool(out poolid, out poolName);
                }

                RunAsSync(async () =>
                {
                    if(facebookUser == null)
                        facebookUser = await FacebookUtilities.CreateFacebookUser(FacebookAppId, FacebookAppSecret);
                });

                _AuthCredentials = new CognitoAWSCredentials(poolid, TestRunner.RegionEndpoint);
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

                _UnauthCredentials = new CognitoAWSCredentials(poolid, TEST_REGION);
                return _UnauthCredentials;
            }
        }

        private AmazonCognitoIdentityClient IdentityClient
        {
            get
            {
                return new AmazonCognitoIdentityClient(TestRunner.Credentials, TestRunner.RegionEndpoint);
            }
        }

        private void CreateIdentityPool(out string poolId, out string poolName)
        {
            string pn = null, pi = null;
            RunAsSync(async () =>
            {
                pn = "netTestPool" + DateTime.Now.ToFileTime();
                var request = new CreateIdentityPoolRequest
                {
                    IdentityPoolName = pn,
                    AllowUnauthenticatedIdentities = true,
                    SupportedLoginProviders = new Dictionary<string, string>() { { FacebookProvider, FacebookAppId } }
                };

                var createPoolResult = await IdentityClient.CreateIdentityPoolAsync(request);
                Assert.IsNotNull(createPoolResult.IdentityPoolId);
                Assert.IsNotNull(createPoolResult.IdentityPoolName);
                Assert.AreEqual(request.AllowUnauthenticatedIdentities, createPoolResult.AllowUnauthenticatedIdentities);
                pi = createPoolResult.IdentityPoolId;
                allPoolIds.Add(pi);

                var describePoolResult = await IdentityClient.DescribeIdentityPoolAsync(new DescribeIdentityPoolRequest
                {
                    IdentityPoolId = pi
                });
                Assert.AreEqual(pi, describePoolResult.IdentityPoolId);
                Assert.AreEqual(pn, describePoolResult.IdentityPoolName);

                var getIdentityPoolRolesResult = await IdentityClient.GetIdentityPoolRolesAsync(new GetIdentityPoolRolesRequest { IdentityPoolId = pi });
                Assert.AreEqual(pi, getIdentityPoolRolesResult.IdentityPoolId);
                Assert.AreEqual(0, getIdentityPoolRolesResult.Roles.Count);

                var roles = new Dictionary<string, string>(StringComparer.Ordinal);
                if ((poolRoles & PoolRoles.Unauthenticated) == PoolRoles.Unauthenticated)
                    roles["unauthenticated"] = PrepareRole();
                if ((poolRoles & PoolRoles.Authenticated) == PoolRoles.Authenticated)
                    roles["authenticated"] = PrepareRole();

                await IdentityClient.SetIdentityPoolRolesAsync(new SetIdentityPoolRolesRequest
                 {
                     IdentityPoolId = pi,
                     Roles = roles
                 });

                getIdentityPoolRolesResult = await IdentityClient.GetIdentityPoolRolesAsync(new GetIdentityPoolRolesRequest { IdentityPoolId = pi });
                Assert.AreEqual(pi, getIdentityPoolRolesResult.IdentityPoolId);
                Assert.AreEqual(NumberOfPoolRoles, getIdentityPoolRolesResult.Roles.Count);
            });
            poolName = pn;
            poolId = pi;
        }

        public async Task<List<IdentityPoolShortDescription>> GetAllPools()
        {
            var request = new ListIdentityPoolsRequest { MaxResults = MaxResults };
            ListIdentityPoolsResponse result;
            List<IdentityPoolShortDescription> l = new List<IdentityPoolShortDescription>();
            do
            {
                result = await IdentityClient.ListIdentityPoolsAsync(request).ConfigureAwait(false);
                foreach (var pool in result.IdentityPools)
                {
                    Assert.IsNotNull(pool);
                    Assert.IsFalse(string.IsNullOrEmpty(pool.IdentityPoolId));
                    Assert.IsFalse(string.IsNullOrEmpty(pool.IdentityPoolName));
                    l.Add(pool);
                }

                request.NextToken = result.NextToken;
            } while (!string.IsNullOrEmpty(result.NextToken));

            return l;
        }

        public async Task<List<IdentityDescription>> GetAllIdentities(string poolId)
        {
            var request = new ListIdentitiesRequest
            {
                MaxResults = MaxResults,
                IdentityPoolId = poolId
            };
            List<IdentityDescription> l = new List<IdentityDescription>();
            ListIdentitiesResponse result;
            do
            {
                result = await IdentityClient.ListIdentitiesAsync(request).ConfigureAwait(false);
                foreach (var ident in result.Identities)
                {
                    Assert.IsNotNull(ident);
                    Assert.IsFalse(string.IsNullOrEmpty(ident.IdentityId));
                    Assert.IsNotNull(ident.Logins);
                    l.Add(ident);
                }
                request.NextToken = result.NextToken;
            } while (!string.IsNullOrEmpty(result.NextToken));
            return l;
        }

        public async Task DeleteIdentityPool(string poolId)
        {
            if (!string.IsNullOrEmpty(poolId))
            {
                var allPools = await GetAllPools().ConfigureAwait(false);
                var pool = allPools.SingleOrDefault(p => string.Equals(poolId, p.IdentityPoolId, StringComparison.Ordinal));

                if (pool != null)
                {
                    Console.WriteLine("Found pool with id [{0}], deleting", poolId);

                    try
                    {
                        await IdentityClient.DeleteIdentityPoolAsync(new DeleteIdentityPoolRequest
                        {
                            IdentityPoolId = poolId
                        }).ConfigureAwait(false);
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

        public async void UpdateIdentityPool(string poolId, string poolName, Dictionary<string, string> providers)
        {
            var updateRequest = new UpdateIdentityPoolRequest
            {
                IdentityPoolName = poolName,
                IdentityPoolId = poolId,
                AllowUnauthenticatedIdentities = true,
            };
            if (providers != null && providers.Count > 0)
                updateRequest.SupportedLoginProviders = providers;

            await IdentityClient.UpdateIdentityPoolAsync(updateRequest).ConfigureAwait(false);
        }


        public string PrepareRole()
        {
            string roleArn = null;

            RunAsSync(async () =>
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

                using (var identityClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient(TestRunner.Credentials, TestRunner.RegionEndpoint))
                {
                    string roleName = "NetWebIdentityRole" + new Random().Next();
                    var response = await identityClient.CreateRoleAsync(new Amazon.IdentityManagement.Model.CreateRoleRequest
                    {
                        AssumeRolePolicyDocument = assumeRolePolicy,
                        RoleName = roleName
                    }).ConfigureAwait(false);

                    await identityClient.PutRolePolicyAsync(new Amazon.IdentityManagement.Model.PutRolePolicyRequest
                    {
                        PolicyDocument = allowPolicy,
                        PolicyName = policyName,
                        RoleName = response.Role.RoleName
                    }).ConfigureAwait(false);

                    roleArn = response.Role.Arn;
                    roleNames.Add(roleName);
                }
            });
            return roleArn;
        }

        public async Task CleanupCreatedRoles()
        {
            foreach (var roleName in roleNames)
            {
                await DeleteRole(roleName).ConfigureAwait(false);
            }
            roleNames.Clear();
        }

        private async Task DeleteRole(string roleName)
        {
            using (var identityClient = new Amazon.IdentityManagement.AmazonIdentityManagementServiceClient(TestRunner.Credentials, TestRunner.RegionEndpoint))
            {
                await identityClient.DeleteRolePolicyAsync(new Amazon.IdentityManagement.Model.DeleteRolePolicyRequest
                {
                    PolicyName = policyName,
                    RoleName = roleName
                }).ConfigureAwait(false);

                await identityClient.DeleteRoleAsync(new Amazon.IdentityManagement.Model.DeleteRoleRequest
                {
                    RoleName = roleName
                }).ConfigureAwait(false);
            }
        }
        #endregion
    }
}
